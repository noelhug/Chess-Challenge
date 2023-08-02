using ChessChallenge.Chess;
using Raylib_cs;
using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ChessChallenge.Application.Settings;
using static ChessChallenge.Application.ConsoleHelper;
using System.Text.Json;
using System.Collections.Generic;
using Board = ChessChallenge.Chess.Board;
using Move = ChessChallenge.Chess.Move;
using Config = ChessChallenge.API.Config;
using ChessChallenge.API;
using ChessChallenge.Example;
using System.Collections;
using static ChessChallenge.Application.ChallengeController;

namespace ChessChallenge.Application
{
    public class ChallengeControllerOptimizer
    {
        public enum PlayerType
        {
            MyBot,
            SFBot,
        }

        readonly Random rng;
        readonly string[] botMatchStartFens;

        public ChallengeControllerOptimizer()
        {
            rng = new Random();
            botMatchStartFens = FileHelper.ReadResourceFile("Fens.txt").Split('\n').Where(fen => fen.Length > 0).ToArray();
        }

        public Config OptimizeConfig()
        {
            int populationSize = 10;
            int generations = 10;
            double mutationRate = 0.05;

            List<Individual> population = InitializePopulation(populationSize);

            Console.WriteLine("Optimizing Configuration:");

            for (int generation = 0; generation < generations; generation++)
            {
                // Update Progress Bar
                DrawProgressBar((double)generation / generations);
                EvaluatePopulation(population);
                List<Individual> selected = SelectConfigurations(population);
                List<Individual> offspring = Crossover(selected, population.Count - selected.Count);
                Mutate(offspring, mutationRate);
                ReplaceWorstConfigurations(population, offspring, population.Count);

            }

            // Ensure 100% completion
            DrawProgressBar(1);

            Config bestConfig = population[0].Config;

            // Write the best configuration to a JSON file
            string json = JsonSerializer.Serialize(bestConfig);
            string path = Path.Combine(Environment.CurrentDirectory, "Resources", "bestResult.json");
            File.WriteAllText(path, json);



            // Write the top 10 configurations to a CSV file
            string csvPath = Path.Combine(Environment.CurrentDirectory, "Resources", "topTenConfigs.csv");
            SaveTopTenConfigsToCsv(population, csvPath);

            Console.WriteLine("\nOptimization complete. Best configuration saved to 'bestResult.json'. Top 10 configurations saved to 'topTenConfigs.csv'.");



            Console.WriteLine("\nOptimization complete. Best configuration saved to 'bestResult.json'.");


            return bestConfig;
        }

        private void SaveTopTenConfigsToCsv(List<Individual> population, string path)
        {
            StringBuilder csvContent = new StringBuilder();
            csvContent.AppendLine("NumOpeningMoves,NumMovesRepeatedPieceMovement,EarlyQueenMovesPenalty,EarlyOverextendingPenalty,EarlyKnighBishopDevelopmentBonus,RepeatedPieceMovePenalty,KnightOnEdgePenalty,RepeatedPositionPenalty,wins,loses,draws");

            int topN = Math.Min(10, population.Count);
            for (int i = 0; i < topN; i++)
            {
                Config config = population[i].Config;
                csvContent.AppendLine($"{config.NumOpeningMoves},{config.NumMovesRepeatedPieceMovement},{config.EarlyQueenMovesPenalty},{config.EarlyOverextendingPenalty},{config.EarlyKnighBishopDevelopmentBonus},{config.RepeatedPieceMovePenalty},{config.KnightOnEdgePenalty},{config.RepeatedPositionPenalty},{population[i].Stats.NumWins},{population[i].Stats.NumLosses},{population[i].Stats.NumDraws}");
            }

            File.WriteAllText(path, csvContent.ToString());
        }

        private void DrawProgressBar(double proportionComplete)
        {
            Console.CursorLeft = 0;
            Console.Write("[");
            int totalBars = 30;
            int numberOfBarsToDraw = (int)(totalBars * proportionComplete);
            Console.Write(new string('=', numberOfBarsToDraw));
            Console.Write(new string(' ', totalBars - numberOfBarsToDraw));
            Console.Write("]");
        }

        private void ReplaceWorstConfigurations(List<Individual> population, List<Individual> offspring, int populationSize)
        {
            // Combine the population and offspring
            List<Individual> combined = new List<Individual>(population);
            combined.AddRange(offspring);

            // Rank the combined list
            combined.Sort(CompareIndividuals);

            // Take the top N individuals, where N is the original population size
            population.Clear();
            population.AddRange(combined.Take(populationSize));
        }

        private void Mutate(List<Individual> offspring, double mutationRate)
        {
            foreach(Individual individual in offspring)
            {
                Config config = individual.Config;

                if (rng.NextDouble() <= mutationRate)
                    config.NumOpeningMoves += randomBool() ? 1 : -1;

                if (rng.NextDouble() <= mutationRate)
                    config.NumMovesRepeatedPieceMovement += randomBool() ? 1 : -1;

                if (randomBool())
                    config.EarlyQueenMovesPenalty += randomBool() ? mutationRate : -mutationRate;

                if (randomBool())
                    config.EarlyOverextendingPenalty += randomBool() ? mutationRate : -mutationRate;

                if (randomBool())
                    config.EarlyKnighBishopDevelopmentBonus += randomBool() ? mutationRate : -mutationRate;

                if (randomBool())
                    config.RepeatedPieceMovePenalty += randomBool() ? mutationRate : -mutationRate;

                if (randomBool())
                    config.KnightOnEdgePenalty += randomBool() ? mutationRate : -mutationRate;

                if (randomBool())
                    config.RepeatedPositionPenalty += randomBool() ? mutationRate : -mutationRate;
            }
        }

        private List<Individual> Crossover(List<Individual> selected, int offspringCount)
        {

            List<Individual> offspring = new List<Individual>();

            // Repeat until enough offspring are created
            for (int i = 0; i < offspringCount; i++)
            {
                Individual parent1 = SelectParent(selected);
                Individual parent2 = SelectParent(selected);

                Individual child = CreateChildFromParents(parent1, parent2);

                offspring.Add(child);
            }

            return offspring;
        }

        private bool randomBool()
        {
            return rng.NextDouble() >= 0.5;
        }

        private Individual CreateChildFromParents(Individual parent1, Individual parent2)
        {
            Config c1 = parent1.Config;
            Config c2 = parent2.Config;

            int NumOpeningMoves = randomBool() ? c1.NumOpeningMoves : c2.NumOpeningMoves;
            int NumMovesRepeatedPieceMovement = randomBool() ? c1.NumMovesRepeatedPieceMovement : c2.NumMovesRepeatedPieceMovement;
            double EarlyQueenMovesPenalty = randomBool() ? c1.EarlyQueenMovesPenalty : c2.EarlyQueenMovesPenalty;
            double EarlyOverextendingPenalty = randomBool() ? c1.EarlyOverextendingPenalty : c2.EarlyOverextendingPenalty;
            double EarlyKnighBishopDevelopmentBonus = randomBool() ? c1.EarlyKnighBishopDevelopmentBonus : c2.EarlyKnighBishopDevelopmentBonus;
            double RepeatedPieceMovePenalty = randomBool() ? c1.RepeatedPieceMovePenalty : c2.RepeatedPieceMovePenalty;
            double KnightOnEdgePenalty = randomBool() ? c1.KnightOnEdgePenalty : c2.KnightOnEdgePenalty;
            double RepeatedPositionPenalty = randomBool() ? c1.RepeatedPositionPenalty : c2.RepeatedPositionPenalty;

            Config createdConfig = new(NumOpeningMoves, NumMovesRepeatedPieceMovement,
                EarlyQueenMovesPenalty, EarlyOverextendingPenalty, EarlyKnighBishopDevelopmentBonus, RepeatedPieceMovePenalty,
                KnightOnEdgePenalty, RepeatedPositionPenalty);

            return new Individual(createdConfig);
        }

        private Individual SelectParent(List<Individual> individuals)
        {
            return individuals[rng.Next(0, individuals.Count - 1)];
        }

        private List<Individual> SelectConfigurations(List<Individual> population)
        {
            population.Sort(CompareIndividuals);

            int survivorsCount = Math.Max((int)(0.3 * population.Count), 1);
            return population.Take(survivorsCount).ToList();
        }

        private int CompareIndividuals(Individual x, Individual y)
        {
            BotMatchStats a = ((Individual)x).Stats;
            BotMatchStats b = ((Individual)y).Stats;

            int scoreA = 10 * a.NumWins + 5 * a.NumDraws;
            int scoreB = 10 * b.NumWins + 5 * b.NumDraws;

            return scoreA - scoreB;
        }

        private void EvaluatePopulation(List<Individual> population)
        {
            var tasks = new List<Task>();
            foreach (Individual individual in population)
            {
                var task = Task.Factory.StartNew(() => EvaluateIndividual(individual), TaskCreationOptions.LongRunning);
                tasks.Add(task);
            }

            Task.WhenAll(tasks).Wait();
        }

        ChessPlayer PlayerToMove(Board board, ChessPlayer white, ChessPlayer black) => board.IsWhiteToMove ? white : black;
        ChessPlayer PlayerNotOnMove(Board board, ChessPlayer white, ChessPlayer black) => board.IsWhiteToMove ? black : white;

        ChessPlayer CreatePlayer(PlayerType type)
        {
            return type switch
            {
                PlayerType.MyBot => new ChessPlayer(new MyBot(), ChallengeController.PlayerType.MyBot, GameDurationMilliseconds),
                PlayerType.SFBot => new ChessPlayer(new SFBot(), ChallengeController.PlayerType.SFBot, GameDurationMilliseconds)
            };
        }

        private void EvaluateIndividual(Individual individual)
        {

            bool isMyBotWhite = true;

            foreach (string fen in botMatchStartFens) {
                Board board = new Board();
                board.LoadPosition(fen);

                ChessPlayer white = isMyBotWhite ? CreatePlayer(PlayerType.MyBot) : CreatePlayer(PlayerType.SFBot);
                ChessPlayer black = isMyBotWhite ? CreatePlayer(PlayerType.SFBot) : CreatePlayer(PlayerType.MyBot);

                bool gameCompleted = false;

                for(int i = 0; i < 2; i++)
                { 
                    while(!gameCompleted)
                    {
                        ChessPlayer playerToMove = PlayerToMove(board, white, black);
                        ChessPlayer playerNotToMove = PlayerNotOnMove(board, white, black);
                        API.Board botBoard = new(board);

                        API.Timer timer = new(playerToMove.TimeRemainingMs, playerNotToMove.TimeRemainingMs, GameDurationMilliseconds, IncrementMilliseconds);
                        API.Move move = white.Bot.Think(botBoard, timer, individual.Config);

                        board.MakeMove(new Move(move.RawValue), false);

                        GameResult result = Arbiter.GetGameState(board);
                        if (Arbiter.IsDrawResult(result))
                        {
                            individual.Stats.NumDraws++;
                            gameCompleted = true;
                        } else if(Arbiter.IsWhiteWinsResult(result) == isMyBotWhite)
                        {
                            individual.Stats.NumWins++;
                            gameCompleted = true;
                        } else if(Arbiter.IsWinResult(result))
                        {
                            individual.Stats.NumLosses++;
                            gameCompleted = true;
                        }
                    }

                    isMyBotWhite = !isMyBotWhite;
                }
            }
        }



        private List<Individual> InitializePopulation(int populationSize)
        {
            var population = new List<Individual>();

            for(int i = 0; i < populationSize; i++)
            {
                population.Add(new Individual(GetConfig()));
            }

            return population;
        }



        private static double GetPseudoDoubleWithinRange(Random random, double lowerBound, double upperBound)
        {
            var rDouble = random.NextDouble();
            var rRangeDouble = rDouble * (upperBound - lowerBound) + lowerBound;
            return rRangeDouble;
        }

        private API.Config GetConfig()
        {
            var random = new Random();

            int NumOpeningMoves = random.Next(0, 100);
            int NumMovesRepeatedPieceMovement = random.Next(0, 100);
            double EarlyQueenMovesPenalty = GetPseudoDoubleWithinRange(random, 0.0, 2.0);
            double EarlyOverextendingPenalty = GetPseudoDoubleWithinRange(random, 0.0, 2.0);
            double EarlyKnighBishopDevelopmentBonus = GetPseudoDoubleWithinRange(random, 0.0, 2.0);
            double RepeatedPieceMovePenalty = GetPseudoDoubleWithinRange(random, 0.0, 2.0);
            double KnightOnEdgePenalty = GetPseudoDoubleWithinRange(random, 0.0, 2.0);
            double RepeatedPositionPenalty = GetPseudoDoubleWithinRange(random, 0.0, 2.0);

            return new(NumOpeningMoves, NumMovesRepeatedPieceMovement,
                EarlyQueenMovesPenalty, EarlyOverextendingPenalty, EarlyKnighBishopDevelopmentBonus, RepeatedPieceMovePenalty,
                KnightOnEdgePenalty, RepeatedPositionPenalty);
        }
    }


    struct Individual
    {
        public Individual(API.Config config) {
            Config = config;
            Stats = new BotMatchStats();
            BotMatchGameIndex = 0;
            BotTaskWaitHandle = new AutoResetEvent(false);
        }

        public int BotMatchGameIndex { get; set; }
        public API.Config Config;
        public BotMatchStats Stats { get; }
        public AutoResetEvent BotTaskWaitHandle { get; }
    }

    public class BotMatchStats
    {
        public int NumWins;
        public int NumLosses;
        public int NumDraws;
        public int NumTimeouts;
        public int NumIllegalMoves;
    }
}

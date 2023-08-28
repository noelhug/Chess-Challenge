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
using System.Diagnostics;

namespace ChessChallenge.Application
{
    public class ChallengeControllerOptimizer
    {
        private readonly Process _stockfishProcess;
        private StreamWriter Ins() => _stockfishProcess.StandardInput;
        private StreamReader Outs() => _stockfishProcess.StandardOutput;

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
            botMatchStartFens = FileHelper
                .ReadResourceFile("Fens.txt")
                .Split('\n')
                .Where(fen => fen.Length > 0)
                .ToArray();

            // Path to the executable
            const string stockfishExe = "/opt/homebrew/bin/stockfish";

            if (stockfishExe != null)
            {
                _stockfishProcess = new Process();
                _stockfishProcess.StartInfo.RedirectStandardOutput = true;
                _stockfishProcess.StartInfo.RedirectStandardInput = true;
                _stockfishProcess.StartInfo.FileName = stockfishExe;
                _stockfishProcess.Start();
                Ins().WriteLine("uci");
                var isOk = false;

                while (Outs().ReadLine() is { } line)
                {
                    if (line != "uciok") continue;
                    isOk = true;
                    break;
                }
                if (!isOk)
                {
                    throw new Exception("Failed to communicate with stockfish");
                }
                Ins().WriteLine($"setoption name Skill Level value 20");
            }
        }

        class CompareTuple : IComparer<Tuple<double, Individual>>
        {
            public int Compare(Tuple<double, Individual> x, Tuple<double, Individual> y)
            {
                int result = y.Item1.CompareTo(x.Item1);
                if (result == 0) return 1;  // Handle duplication
                return result;
            }
        }

        public Config OptimizeConfig()
        {
            int N = 100;
            SortedSet<Tuple<double, Individual>> minStack = new SortedSet<Tuple<double, Individual>>(new CompareTuple());
            object syncLock = new object();

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = 8 };
            Parallel.For(0, N, options, i =>
            {
                var config = GetConfig();
                var individual = new Individual(config);
                var evaluation = EvaluateIndividual(individual);

                individual.AverageCentipawnLoss = evaluation;


                lock (syncLock)
                {
                    // We multiply evaluation by -1 to simulate a min stack since SortedSet sorts in ascending order
                    minStack.Add(Tuple.Create(-evaluation, individual));
                    WriteConfigToCsv(individual);  // Write the individual's config to CSV
                }
            });

            Console.WriteLine("Bottom 10 Individuals:");
            int count = 0;
            foreach (var tuple in minStack)
            {
                if (count++ == 10) break;
                Console.WriteLine("Config: \n" + tuple.Item2.Config.ToString());
                Console.WriteLine("Average Centipawn Loss: " + -tuple.Item1);
            }

            return minStack.Last().Item2.Config;
        }

        private void WriteConfigToCsv(Individual individual, string filePath = "configs.csv")
        {
            // If file doesn't exist, create it and write the header
            if (!File.Exists(filePath))
            {
                string header = "NumOpeningMoves,NumMovesRepeatedPieceMovement,EarlyQueenMovesPenalty,EarlyOverextendingPenalty,EarlyKnighBishopDevelopmentBonus,RepeatedPieceMovePenalty,KnightOnEdgePenalty,RepeatedPositionPenalty,AverageCentipawnLoss";
                File.WriteAllText(filePath, header + Environment.NewLine);
            }

            // Create a line for the current individual's config
            string line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                individual.Config.NumOpeningMoves,
                individual.Config.NumMovesRepeatedPieceMovement,
                individual.Config.EarlyQueenMovesPenalty,
                individual.Config.EarlyOverextendingPenalty,
                individual.Config.EarlyKnighBishopDevelopmentBonus,
                individual.Config.RepeatedPieceMovePenalty,
                individual.Config.KnightOnEdgePenalty,
                individual.Config.RepeatedPositionPenalty,
                individual.AverageCentipawnLoss
            );

            // Append the line to the file
            File.AppendAllText(filePath, line + Environment.NewLine);
        }

        private double GetStockfishEvaluation(Board board, API.Timer timer)
        {
            try
            { 
                Ins().WriteLine("ucinewgame");
                Ins().WriteLine($"position fen {board.RepetitionPositionHistoryFen.Peek()}");
                var timeString = board.IsWhiteToMove ? "wtime" : "btime";
                Ins().WriteLine($"go depth 1");
                double? evalScore = null;

                while (Outs().ReadLine() is { } line)
                {
                    if (line.Contains("score cp "))
                    {
                        var parts = line.Split();
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i] == "score" && parts[i + 1] == "cp" && i + 2 < parts.Length)
                            {
                                if (double.TryParse(parts[i + 2], out double score))
                                {
                                    evalScore = score; // Convert centipawns to pawns.
                                    break;
                                }
                            }
                        }
                    }

                    if (line.StartsWith("bestmove"))
                    {
                        // Exit loop when Stockfish provides the best move.
                        break;
                    }
                }

                if (!evalScore.HasValue)
                {
                    return 0;
                }
                return evalScore.Value;
            } catch (Exception)
            {
                return 0;
            }
        }


        public double EstimateElo(double acpl)
        {
            return 3100 * Math.Exp(-0.01 * acpl);
        }


        private List<Individual> EvaluatePopulation(List<Individual> population)
        {
            var tasks = new List<Task>();
            List<Individual> result = new List<Individual>();
            for (int i = 0; i < population.Count; i++)
            {
                Individual individual = population[i];
                individual.Stats = new(); 
                var task = Task.Factory.StartNew(
                    () => { individual.AverageCentipawnLoss = EvaluateIndividual(individual); result.Add(individual); },
                    TaskCreationOptions.LongRunning
                );
                tasks.Add(task);
            }

            Task.WhenAll(tasks).Wait();

            return result;
        }

        ChessPlayer PlayerToMove(Board board, ChessPlayer white, ChessPlayer black) =>
            board.IsWhiteToMove ? white : black;

        ChessPlayer PlayerNotOnMove(Board board, ChessPlayer white, ChessPlayer black) =>
            board.IsWhiteToMove ? black : white;

        ChessPlayer CreatePlayer(PlayerType type)
        {
            return type switch
            {
                PlayerType.MyBot
                    => new ChessPlayer(
                        new MyBot(),
                        ChallengeController.PlayerType.MyBot,
                        GameDurationMilliseconds
                    ),
                PlayerType.SFBot
                    => new ChessPlayer(
                        new SFBot(),
                        ChallengeController.PlayerType.SFBot,
                        GameDurationMilliseconds
                    )
            };
        }

        private double EvaluateIndividual(Individual individual)
        {
            bool isMyBotWhite = true;

            double previousEval = 0; 
            double totalCentipawnLoss = 0;
            double moveCount = 0.0;


            foreach (string fen in botMatchStartFens)
            {

                ChessPlayer white = isMyBotWhite
                    ? CreatePlayer(PlayerType.MyBot)
                    : CreatePlayer(PlayerType.SFBot);
                ChessPlayer black = isMyBotWhite
                    ? CreatePlayer(PlayerType.SFBot)
                    : CreatePlayer(PlayerType.MyBot);

                bool gameCompleted = false;

                for (int i = 0; i < 2; i++)
                {
                    Board board = new Board();
                    board.LoadPosition(fen);
                    while (!gameCompleted)
                    {
                        ChessPlayer playerToMove = PlayerToMove(board, white, black);
                        ChessPlayer playerNotToMove = PlayerNotOnMove(board, white, black);
                        API.Board botBoard = new(board);

                        API.Timer timer =
                            new(
                                playerToMove.TimeRemainingMs,
                                playerNotToMove.TimeRemainingMs,
                                GameDurationMilliseconds,
                                IncrementMilliseconds
                            );
                        API.Move move = playerToMove.Bot.Think(botBoard, timer, individual.Config);

                        board.MakeMove(new Move(move.RawValue), false);


                        // If the bot that just moved is MyBot, get the evaluation and compute the centipawn loss.
                        if (playerToMove.Bot is MyBot)
                        {
                            double currentEval = GetStockfishEvaluation(board, timer);
                            totalCentipawnLoss += Math.Abs(currentEval - previousEval); // Convert pawn difference to centipawns.
                            moveCount++;
                            previousEval = currentEval;
                        }

                        GameResult result = Arbiter.GetGameState(board);
                        if (Arbiter.IsDrawResult(result))
                        {
                            individual.Stats.NumDraws++;
                            gameCompleted = true;
                        }
                        else if (Arbiter.IsWhiteWinsResult(result) == isMyBotWhite)
                        {
                            individual.Stats.NumWins++;
                            gameCompleted = true;
                        }
                        else if (Arbiter.IsWinResult(result))
                        {
                            individual.Stats.NumLosses++;
                            gameCompleted = true;
                        }
                    }

                    isMyBotWhite = !isMyBotWhite;
                }
            }
            double acpl = totalCentipawnLoss / moveCount;
            individual.AverageCentipawnLoss = acpl; 
            Console.WriteLine($"Average Centipawn Loss (ACPL) for this individual: {individual.AverageCentipawnLoss}");
            return acpl;
        }

        private List<Individual> InitializePopulation(int populationSize)
        {
            var population = new List<Individual>(populationSize);

             for (int i = 0; i < populationSize; i++)
            {
                population.Add(new Individual(GetConfig()));
            }

            return population;
        }

        private static double GetPseudoDoubleWithinRange(
            Random random,
            double lowerBound,
            double upperBound
        )
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

            return new(
                NumOpeningMoves,
                NumMovesRepeatedPieceMovement,
                EarlyQueenMovesPenalty,
                EarlyOverextendingPenalty,
                EarlyKnighBishopDevelopmentBonus,
                RepeatedPieceMovePenalty,
                KnightOnEdgePenalty,
                RepeatedPositionPenalty
            );
        }
    }

    struct Individual
    {
        public Individual(API.Config config)
        {
            Config = config;
            Stats = new BotMatchStats();
            BotMatchGameIndex = 0;
            AverageCentipawnLoss = 0;
            BotTaskWaitHandle = new AutoResetEvent(false);
        }

        public int BotMatchGameIndex { get; set; }
        public API.Config Config;
        public double AverageCentipawnLoss { get; set; }
        public BotMatchStats Stats { get; set; }
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

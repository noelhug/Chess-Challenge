using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private ulong[] weightsAndBiasEncoded = new ulong[]
    {
        0x302e303230333230, 0x3736353637343131, 0x343232372c302e30, 0x3231303439333234, 0x3432333037343732,
        0x322c302e30323239, 0x3432343831353632, 0x34393532332c302e, 0x3032323738383131, 0x3131323034363234,
        0x31382c302e303232, 0x3935373933323230, 0x343030383130322c, 0x302e303232343139, 0x3230383636303732,
        0x3137382c302e3032, 0x3337363535353237, 0x3838393732383535, 0x2c302e3032303739, 0x3433393334393437,
        0x32353232372c302e, 0x3032303838303937, 0x3131303339303636, 0x332c302e30323433, 0x3438363931313035,
        0x38343235392c302e, 0x3032313735333431, 0x3336303237303937, 0x372c302e30323036, 0x3336373633343233,
        0x36383132362c302e, 0x3032313234373330, 0x3638333836333136, 0x332c302e30323337, 0x3133303431303936,
        0x3932353733352c30, 0x2e30323336383039, 0x3939383735303638, 0x3636352c302e3032, 0x3233373830343233,
        0x3430323738363235, 0x2c302e3032313337, 0x3634393234353536, 0x30313639322c302e, 0x3032333538333638,
        0x3431313636303139, 0x34342c302e303230, 0x3433303638373831, 0x343935303934332c, 0x302e303235383534,
        0x3438353130393434, 0x383433332c302e30, 0x3232393331333133, 0x3134323138303434, 0x332c302e30323231,
        0x3136363739363938, 0x3232383833362c30, 0x2e30323232393937, 0x3934343830323034, 0x3538322c302e3032,
        0x3431323731393436, 0x353739323137392c, 0x302e303230313536, 0x3639303835303835, 0x3339322c302e3032,
        0x3032393038333131, 0x3033393230393337, 0x2c302e3032323532, 0x3336343930333638, 0x38343330382c302e,
        0x3032353831333132, 0x3530373339303937, 0x362c302e30323139, 0x3137333030323938, 0x3831303030352c30,
        0x2e30323236393237, 0x3330363530333035, 0x3734382c302e3032, 0x3438363634393731, 0x343431303330352c,
        0x302e303237323934, 0x3330323335393232, 0x333336362c302e30, 0x3234313533373434, 0x3830313837383933,
        0x2c302e3032323238, 0x3933343837363632, 0x30373639352c302e, 0x3032303737343233, 0x3232323336323939,
        0x352c302e30323536, 0x3436373438303231, 0x3234353030332c30, 0x2e30323230303938, 0x3430323335313134,
        0x3039382c302e3032, 0x3338323933343833, 0x3835333334303135, 0x2c302e3032313934, 0x3739353031323437,
        0x3430362c302e3032, 0x3430353836313736, 0x3531343632353535, 0x2c302e3032313535, 0x3030393437303838,
        0x30303331362c302e, 0x3032303638373236, 0x3334353839363732, 0x312c302e30323238, 0x3132303634373337,
        0x3038313532382c30, 0x2e30323538333437, 0x3936393530323231, 0x30362c302e303234, 0x3835383930363836,
        0x353131393933342c, 0x302e303232353038, 0x3630383137373330, 0x343236382c302e30, 0x3233313335363238,
        0x3535313234343733, 0x362c302e30323232, 0x3235373135323139, 0x3937343531382c30, 0x2e30313935323839,
        0x3038363535303437, 0x3431372c302e3032, 0x3430343034313032, 0x3935313238383232, 0x2c302e3032303035,
        0x3334303930333939, 0x37343231332c302e, 0x3032333539313131, 0x3630373037343733, 0x37352c302e303232,
        0x3437333731373130, 0x383336383837342c, 0x302e303231343030, 0x3331373534393730, 0x353530352c302e30,
        0x3233393130373231, 0x3736333936383436, 0x382c302e30313939, 0x3833323639323734, 0x32333437372c302e,
        0x3032303638313630, 0x3834363832393431, 0x34342c302e303139, 0x3032363333313630, 0x3335323730372c30,
        0x2e30323339333030, 0x3933323733353230, 0x34372c302e303232, 0x3435343931393239, 0x333532323833352c,
        0x302e303232353731, 0x3635343939303331, 0x353433372c302e30, 0x3230373637323139, 0x3336343634333039,
        0x372c302e30323138, 0x3336333130363235, 0x3037363239342c30, 0x2e30313938383633, 0x3432383038363034,
        0x32342c2d302e3030, 0x3739383435383937, 0x3835333337343438
    };

    private double[] weights = new double[64];
    private double[] biases = new double[1];
    private LinearModel model = null;

    public void decodeWeightsAndBias()
    {
        if (weights[0] != 0.0) return; // We already have decoded the weights

        StringBuilder huffmanDecoded = new StringBuilder();

        foreach (ulong hexNumber in weightsAndBiasEncoded)
        {
            byte[] bytes = BitConverter.GetBytes(hexNumber);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes); // ensures bytes are in the correct order

            string ascii = Encoding.ASCII.GetString(bytes);
            huffmanDecoded.Append(ascii);
        }

        // Split string into separate values
        string[] values = huffmanDecoded.ToString().Split(',');

        // Convert first 64 values to weights
        for (int i = 0; i < 64; i++)
        {
            weights[i] = double.Parse(values[i]);
        }

        // Convert the remaining values to biases
        for (int i = 64; i < values.Length; i++)
        {
            biases[i - 64] = double.Parse(values[i]);
        }

        model = new LinearModel(weights, biases[0]);
    }

    public Move Think(Board board, Timer timer)
    {
        decodeWeightsAndBias();
        Move[] moves = board.GetLegalMoves();
        Move bestMove = moves[0];
        double alpha = double.MinValue;
        double beta = double.MaxValue;
        double maxEval = double.MinValue;
        double minEval = double.MaxValue;


        foreach (Move move in moves)
        {
            board.MakeMove(move);

            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                bestMove = move;
                break;
            }
            
            double eval = AlphaBeta(board, 4, alpha, beta, board.IsWhiteToMove);

            if (!board.IsWhiteToMove)
            {
                if (eval > maxEval)
                {
                    maxEval = eval;
                    bestMove = move;
                }
            }
            else
            {
                if (eval < minEval)
                {
                    minEval = eval;
                    bestMove = move;
                }
            }

            alpha = Math.Max(alpha, eval);
            board.UndoMove(move);
        }
        
        
        return bestMove;
    }


    private double AlphaBeta(Board node, int depth, double alpha, double beta, bool isMaximizingPlayer)
    {
        if (depth == 0 || node.GetLegalMoves().Length == 0)
        {
            return Evaluate(node, isMaximizingPlayer);
        }

        if (isMaximizingPlayer)
        {
            double maxEval = int.MinValue;
            foreach (Move child in node.GetLegalMoves())
            {
                node.MakeMove(child);
                
                double eval = AlphaBeta(node, depth - 1, alpha, beta, false);
                maxEval = Math.Max(maxEval, eval);
                alpha = Math.Max(alpha, eval);
                
                node.UndoMove(child);
                if (beta <= alpha)
                    break;
            }

            return maxEval;
        }
        else
        {
            double minEval = int.MaxValue;
            foreach (Move child in node.GetLegalMoves())
            {
                node.MakeMove(child);

                double eval = AlphaBeta(node, depth - 1, alpha, beta, true);
                minEval = Math.Min(minEval, eval);
                beta = Math.Min(beta, eval);
                node.UndoMove(child);
                if (beta <= alpha)
                    break;
            }

            return minEval;
        }
    }

    
    private double[] GetBoardArrayFromBoard(Board board)
    {
        try
        {
            double[] pieces = new double[64];
            for (int i = 0; i < 64; i++)
            {
                Piece piece = board.GetPiece(new Square(i));
                if (piece != null)
                {
                    int pieceVal = GetPieceValue(piece);
                    pieceVal = piece.IsWhite ? pieceVal : -pieceVal;
                    pieces[i] = pieceVal;
                }
                else
                {
                    pieces[i] = 0;
                }
            }
            return pieces;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception occurred: {e.Message}");
            return null;
        }
    }

    private int GetPieceValue(Piece piece)
    {
        switch (piece.PieceType)
        {
            case PieceType.Pawn:
                return 1;
            case PieceType.Bishop:
                return 2;
            case PieceType.Knight:
                return 3;
            case PieceType.Rook:
                return 4;
            case PieceType.Queen:
                return 5;
            case PieceType.King:
                return 6;
            default:
                return 0;
        }
    }

    private double Evaluate(Board board, bool isBotWhite)
    {
        return model.Predict(GetBoardArrayFromBoard(board)); // This is just an example.
    }
}

public class LinearModel
{
    private double[] Weights { get; }
    private double Bias { get; }

    public LinearModel(double[] weights, double bias)
    {
        Weights = weights;
        Bias = bias;
    }

    public double Predict(double[] inputs)
    {
        double result = Bias;

        for (int i = 0; i < Weights.Length; i++)
        {
            result += Weights[i] * inputs[i];
        }
        return result;
    }
}
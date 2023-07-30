
from multiprocessing import Pool
import pandas as pd
import chess.engine
from tqdm import tqdm


# Load your dataframe
df = pd.read_csv('games2.csv')

# Function to analyze a single game
def analyze_game(moves):
    engine = chess.engine.SimpleEngine.popen_uci('/opt/homebrew/bin/stockfish')
    board = chess.Board()

    evaluations = []
    
    move_list = moves.split()
    for move in move_list:
        # Make the move on the board
        board.push_san(move)

        # Ask stockfish to evaluate the board position
        info = engine.analyse(board, chess.engine.Limit(time=0.1))
        
        # Same evaluation handling as before...
        if "score" in info:
            if isinstance(info["score"].relative, chess.engine.Mate):
                evaluation = 'Mate in {}'.format(info["score"].relative.mate)
            else:
                evaluation = info["score"].relative.cp
                if board.turn == chess.BLACK:
                    evaluation = -evaluation if evaluation is not None else evaluation
        else:
            evaluation = 'unknown'
        
        evaluations.append((board.fen(), evaluation))

    engine.quit()
    return evaluations

if __name__ == '__main__':
    # Create a pool of worker processes
    with Pool() as p:
        # Create a progress bar
        with tqdm(total=len(df)) as pbar:
            # Analyze all games in parallel
            results = []
            for result in p.imap_unordered(analyze_game, df['moves']):
                results.append(result)
                # Update the progress bar
                pbar.update()

    # Flatten the list of lists and convert it into a dataframe
    flat_results =  [item for sub in results for item in sub]
    result_df = pd.DataFrame(flat_results, columns=["Position", "Evaluation"])

    # Save the result dataframe to a csv file
    result_df.to_csv(f'/Volumes/data/evaluated_positions_{len(df)}.csv', index=False)

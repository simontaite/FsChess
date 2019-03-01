namespace FsChess

module Domain =

    type Colour = | White | Black

    type Column = | A | B | C | D | E | F | G | H
        with static member List = [ A; B; C; D; E; F; G; H]
    type Row = | One | Two | Three | Four | Five | Six | Seven | Eight
        with static member List = [ One; Two; Three; Four; Five; Six; Seven; Eight]

    type Square = (Column * Row)

    type PawnState = | NotMoved | Moved

    type Rank =
        | Pawn of PawnState
        | Rook
        | Knight
        | Bishop
        | Queen
        | King

    type Piece = { Player : Colour; Rank : Rank }
    type Board = Map<Square, Piece option>
    type ProposedMove = { From : Square; To : Square }
    type ProposedMoveWithKnownPiece = { SelectedPiece : Piece; From : Square; To : Square }
    type ValidatedMove = { Piece : Piece; From : Square; To : Square; CapturedPiece : Piece option }
    type GameStatus = | InProgress | WhiteInCheck | WhiteInCheckmate | BlackInCheck | BlackInCheckmate | Stalemate
    type GameState = { Board : Board; CurrentPlayer : Colour; Status : GameStatus; Message : string; MoveHistory : ValidatedMove list }
    type Distance = { Horizontal : int; Vertical : int }

    let initialiseGame () =

        let blackPawn = Some { Player = Black; Rank = Pawn NotMoved }
        let whitePawn = Some { Player = White; Rank = Pawn NotMoved }

        let black rank = Some { Player = Black; Rank = rank }
        let white rank = Some { Player = White; Rank = rank }

        let createRow row pieces =
            let cells =  Column.List |> List.map (fun col -> (col, row))
            List.zip cells pieces

        let board =
            Map (   (createRow Eight    [black Rook;   black Knight;   black Bishop;   black Queen;    black King;    black Bishop;   black Knight;   black Rook]) @
                    (createRow Seven    [blackPawn;    blackPawn;      blackPawn;      blackPawn;      blackPawn;     blackPawn;      blackPawn;      blackPawn]) @
                    (createRow Six      [None;         None;           None;           None;           None;          None;           None;           None]) @
                    (createRow Five     [None;         None;           None;           None;           None;          None;           None;           None]) @
                    (createRow Four     [None;         None;           None;           None;           None;          None;           None;           None]) @
                    (createRow Three    [None;         None;           None;           None;           None;          None;           None;           None]) @
                    (createRow Two      [whitePawn;    whitePawn;      whitePawn;      whitePawn;      whitePawn;     whitePawn;      whitePawn;      whitePawn]) @
                    (createRow One      [white Rook;   white Knight;   white Bishop;   white Queen;    white King;    white Bishop;   white Knight;   white Rook]) )

        let player =  White

        {   Board = board
            CurrentPlayer = player
            Status = InProgress
            Message = sprintf "%A to move" player
            MoveHistory = [] }

    let updateGameState (gameState : GameState) move : GameState =
        let p =
            match move.Piece.Rank with
            | Pawn NotMoved -> { move.Piece with Rank = Pawn Moved }
            | _ -> move.Piece

        let board = gameState.Board.Add(move.From, None).Add(move.To, Some p)

        let nextPlayer = match gameState.CurrentPlayer with | White -> Black | Black -> White

        {
            Board = board
            CurrentPlayer = nextPlayer
            Status = gameState.Status
            Message = sprintf "%A to move" nextPlayer
            MoveHistory = move :: gameState.MoveHistory
        }

module Moves =

    open Result
    open Domain

    let getDistance (move : ProposedMoveWithKnownPiece) =

        let fromX, fromY = move.From
        let toX, toY = move.To

        let deltaX = (Column.List |> List.findIndex (fun c -> c = toX)) - (Column.List |> List.findIndex (fun c -> c = fromX))
        let deltaY = (Row.List |> List.findIndex (fun c -> c = toY)) - (Row.List |> List.findIndex (fun c -> c = fromY))

        { Horizontal = deltaX; Vertical = deltaY}

    let markMoveAsValidated (board : Board) (move : ProposedMoveWithKnownPiece) =
        Ok { Piece = move.SelectedPiece; From = move.From; To = move.To; CapturedPiece = board.[move.To] }

    let validatePieceSelected (board : Board) (move : ProposedMove) =
        match board.[move.From] with
        | Some p -> Ok { SelectedPiece = p; From = move.From; To = move.To }
        | None -> Error "You must select a piece"


    let validatePieceIsGood playerToMove (move : ProposedMoveWithKnownPiece) =
        match (playerToMove = move.SelectedPiece.Player) with
        | true -> Ok move
        | false -> Error "You cannot move another player's piece"

    let validateNoFriendlyFire (board : Board) playerToMove (move : ProposedMoveWithKnownPiece) =

        let targetPiece = board.[move.To]

        match targetPiece with
        | Some p ->
            match playerToMove = p.Player with
            | true -> Error "You cannot capture your own piece"
            | false -> Ok move
        | None -> Ok move

    let validateMoveForPiece (board : Board) player (move : ProposedMoveWithKnownPiece) =

        let isValidPawnNotMoved (board : Board) player move =
            let distance = getDistance move
            let direction = match player with | White -> 1 | Black -> -1
            let target = board.[move.To]

            match ((abs distance.Horizontal), (distance.Vertical), target) with
            | (x, y, None) when x = 0 && y = (1 * direction) -> Ok move
            | (x, y, None) when x = 0 && y = (2 * direction) -> Ok move
            | (x, y, Some _) when x = 1 && y = (1 * direction) -> Ok move
            | (x, y, None) when x = 1 && y = (2 * direction) ->
                Ok move // en passant to be handled here
            | _ -> Error ""

        let isValidPawnMoved (board : Board) player move =
            let distance = getDistance move
            let direction = match player with | White -> 1 | Black -> -1
            let target = board.[move.To]

            match ((abs distance.Horizontal), (distance.Vertical), target) with
            | (x, y, None) when x = 0 && y = (1 * direction) -> Ok move
            | (x, y, Some _) when x = 1 && y = (1 * direction) -> Ok move
            | _ -> Error ""

        let isStraightLine move =
            let distance = getDistance move
            match ((abs distance.Horizontal), (abs distance.Vertical)) with
            | (x, y) when x > 0 && y = 0 -> Ok move
            | (x, y) when x = 0 && y > 0 -> Ok move
            | _ -> Error ""

        let isLShape move =
            let distance = getDistance move
            match ((abs distance.Horizontal), (abs distance.Vertical)) with
            | (2, 1) -> Ok move
            | (1, 2) -> Ok move
            | _ -> Error ""

        let isDiagonal move =
            let distance = getDistance move
            match ((abs distance.Horizontal), (abs distance.Vertical)) with
            | (x, y) when x > 0 && y = x -> Ok move
            | _ -> Error ""

        let isStraightLineOrDiagonal move =
            isStraightLine move >>= isDiagonal

        let isOneSquareInAnyDirection move =
            let distance = getDistance move
            match ((abs distance.Horizontal), (abs distance.Vertical)) with
            | (x, y) when x = 1 && y = 0 -> Ok move
            | (x, y) when x = 0 && y = 1 -> Ok move
            | (x, y) when x = 1 && y = 1 -> Ok move
            | _ -> Error ""

        match move.SelectedPiece.Rank with
        | Pawn NotMoved -> isValidPawnNotMoved board player move
        | Pawn Moved -> isValidPawnMoved board player move
        | Rook -> isStraightLine move
        | Knight -> isLShape move
        | Bishop -> isDiagonal move
        | Queen -> isStraightLineOrDiagonal move
        | King -> isOneSquareInAnyDirection move

    let validateMove (gameState : GameState) (move : ProposedMove) =

        result {
            return!
                validatePieceSelected gameState.Board move
                >>= validatePieceIsGood gameState.CurrentPlayer
                >>= validateNoFriendlyFire gameState.Board gameState.CurrentPlayer
                // All move validation goes here
                >>= validateMoveForPiece gameState.Board gameState.CurrentPlayer
                >>= markMoveAsValidated gameState.Board
        }

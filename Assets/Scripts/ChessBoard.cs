using System;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class ChessBoard : MonoBehaviour
{
    [Header("Art Stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCentre = Vector3.zero;
    [SerializeField] private float deathSize = 0.005f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 0.5f;
    [SerializeField] private GameObject victoryScreen;

    [Header("Prefabs && Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //Logic
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    //Multiplayer logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    
    //Awake and Update Functions
    private void Start()
    {
        isWhiteTurn = true;
        
        GenarateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvent();
    }
    private void Update()
    {
        if(!currentCamera) 
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile","Hover","Highlight")))
        {
            // get the indices of tile that i've hit
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            //If we're hovering a tile after not hovering any tiles 
            if(currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If we are already hovering a tile, change the previous one
            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover))
                    ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // if we press down on the mouse
            if(Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    //is it our turn
                    if((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || 
                        (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        // Get a list of where i can go, highlight tiles as well
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces,TILE_COUNT_X,TILE_COUNT_Y);
                        // Get a list of Special Moves as well
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }

            // if we are releasing the mouse button
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);
                if (!validMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                    currentlyDragging = null;
                }
                currentlyDragging = null;
                RemoveHighlightTiles();

            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) 
                    ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if(currentlyDragging && Input.GetMouseButtonDown(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        //if currently dragging
        if(currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance)) 
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }

    //Generate the Board
    private void GenarateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountY / 2) * tileSize) + boardCentre;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            }
        }
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0},Y:{1}",x,y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //Spawning of the Pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0, blackTeam = 1;

        //White Team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
        }

        //Black Team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
        }
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type,int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    //Positioning
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
            }
        }
    }
    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }
    private Vector3 GetTileCenter (int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2);
    }

    //Highlighting tiles
    private void HighlightTiles()
    {
        for(int i=0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }

    //CheckMate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnResetButton()
    {
        //UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        //Fields
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        //Cleanup
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for(int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x ,y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();

        //Spawning Again
        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
    }
    public void OnExitButton()
    {
        Application.Quit();
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        //En Passant
        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPostion = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPostion[1].x, targetPawnPostion[1].y];

            if(myPawn.currentX == enemyPawn.currentX)
            {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if(enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.forward * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        //Promotion
        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn)
            {
                //White Promotion
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                //Black Promotion
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        //Castling
        if(specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            //Left Rook
            if (lastMove[1].x == 2)
            {
                //White Side
                if(lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                //Black Side
                else if(lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            //Right Rook
            else if(lastMove[1].x == 6)
            {
                //White Side
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                //Black Side
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for(int x = 0; x< TILE_COUNT_X; x++)
        {
            for(int y = 0; y< TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].type == ChessPieceType.King)
                    {
                        if (chessPieces[x, y].team == currentlyDragging.team)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                }
            }
        }

        //We're sending ref of availableMoves, we'll delete all the moves that put us in check position
        SimulateMoveForSinglePiece(currentlyDragging,ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        //Save current values, to reset after the function call
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        //Going through all the moves, simulate them and check if we're in 'CHECK'
        for(int i=0; i< moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            //Did we simulate the king's move
            if (cp.type == ChessPieceType.King)
            {
                kingPositionThisSim = new Vector2Int(simX, simY);
            }

            //Copy the [,] and not a reference
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttackingPiece = new List<ChessPiece>();
            for(int x = 0; x < TILE_COUNT_X; x++)
            {
                for(int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                        {
                            simAttackingPiece.Add(simulation[x , y]);
                        }
                    }
                }
            }

            //Simulate that move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            //Did one of our Piece got taken down during our simulation
            var deadPiece = simAttackingPiece.Find(c => c.currentX == simX && c.currentY == simY);
            if(deadPiece != null )
            {
                simAttackingPiece.Remove(deadPiece);
            }

            //Get all the simulated attacking pieces moves
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for(int a = 0; a< simAttackingPiece.Count; a++)
            {
                var pieceMoves = simAttackingPiece[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for(int b = 0; b< pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }

            //Is the king in trouble? if so, remove the move
            if(ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }

            //Restore the actual cp data
            cp.currentX = actualX;
            cp.currentY = actualY;
        }
        
        //Remove moves from current available move list
        for(int i=0; i< movesToRemove.Count; i++)
        {
            moves.Remove(movesToRemove[i]);
        }
    }
    private bool CheckForCheckMate()
    {
        var lastMove = moveList[moveList.Count-1];
        int targetTeam = (chessPieces[lastMove[1].x , lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();

        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                        {
                            targetKing = chessPieces[x, y];
                        }

                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }
            }
        }

        //Is the king attcked right now?
        List<Vector2Int> currentAvailabeMoves = new List<Vector2Int>();
        for(int i = 0; i <attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailabeMoves.Add(pieceMoves[b]);
            }
        }

        //Are we in check right now
        if(ContainsValidMove(ref currentAvailabeMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            //King is under attck can we move something to help him?
            for(int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                //We're sending ref of availableMoves, we'll delete all the moves that put us in check position
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if(defendingMoves.Count != 0)
                    return false;
            }

            //Checkmate Exit
            return true;
        }

        return false;
    }

    //Operations
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if(!ContainsValidMove(ref availableMoves,new Vector2Int(x, y))) 
            return false;
        
        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);

        //Is there another piece on target position?
        if (chessPieces[x,y] != null)
        {
            ChessPiece ocp = chessPieces[x,y];

            if(cp.team == ocp.team)
            {

                return false;
            }

            //if its the enemy team
            if(ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);
                
                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2) 
                    + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y,false);

        isWhiteTurn = !isWhiteTurn;
        if(localGame)
        {
            currentTeam = (currentTeam == 0) ? 1 : 0;
        }
        moveList.Add(new Vector2Int[] {previousPosition, new Vector2Int(x, y)});

        ProcessSpecialMove();

        if (CheckForCheckMate())
            CheckMate(cp.team);

        return true;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for ( int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (tiles[x,y] == hitInfo)
                    return new Vector2Int(x,y);
            }
        }
        return -Vector2Int.one;//Invalid (-1, -1)
    }
    private bool ContainsValidMove (ref List<Vector2Int> moves, Vector2Int pos)
    {
        for(int i=0; i<moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        }
        return false;
    }

    #region
    private void RegisterEvent()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }
    private void UnregisterEvent()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }

    //Server
    private void OnWelcomeServer(NetMessages msg, NetworkConnection cnn)
    {
        //Client has connected, Assign a team and return the message back to client
        NetWelcome nw = msg as NetWelcome;

        //Assign a team
        nw.AssignedTeam = ++playerCount;

        //Return back to the client
        Server.Instance.SendToClient(cnn , nw);

        //If full start the game
        if(playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }

    //Client
    private void OnWelcomeClient(NetMessages msg)
    {
        //Receive the connection message
        NetWelcome nw = msg as NetWelcome;

        //Assign the team
        currentTeam = nw.AssignedTeam;

        Debug.Log($"My Assigned team is {nw.AssignedTeam}");

        if(localGame && currentTeam == 0) 
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnStartGameClient(NetMessages msg)
    {
        //We just need to change the Camera
        GameUI.Instance.ChangeCamera((currentTeam == 0) ?CameraAngles.whiteTeam :CameraAngles.blackTeam);
    }

    //LocalGame
    private void OnSetLocalGame(bool v)
    {
        localGame = v;
    }
    #endregion
}

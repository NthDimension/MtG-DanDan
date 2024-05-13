using UnityEngine;
using Unity.Netcode;


//Stores the State of the game

public class GameData : NetworkBehaviour
{
    //Things to do with cards(the class)

    //Null Card
    public Card nullCard = new(255, 255, 255, 255, null, null); //NullCard
    


//________________________________________________________________
    //Things to do with storing the cards
    public Card[][,] GS = new Card[8][ , ]  {
          new Card[1, GLC.MAX_DECK_SIZE],
          new Card[2, GLC.MAX_MAX_HAND_SIZE],
          new Card[1, GLC.maxStackSize],
          new Card[2, GLC.maxCreatures],
          new Card[2, GLC.maxLands],
          new Card[1, GLC.maxGraveyard],
          new Card[1, GLC.maxExile],
          new Card[1, GLC.maxMovableCards]
      };


    public byte[][] numCards = new byte[8][] {
        new byte[1] {GLC.MAX_DECK_SIZE},    //Deck
        new byte[2] {0, 0},                 //Hand
        new byte[1] {0},                    //Stack
        new byte[2] {0, 0},                 //Creatures
        new byte[2] {0, 0},                 //Lands
        new byte[1] {0},                    //Graveyard
        new byte[1] {0},                    //Exile
        new byte[1] {0}                     //Movable
    };




//_____________________________________________________________
    //Things to do with life totals

    //Stores the life totals for each player
    public int[] lifeTotal = new int[2];


//_______________________________________________________________
    //The Stack/Priority


    //  public Card[] theStack = new Card[GLC.maxStackSize];

    //  public byte stackSize = 0;

    public NetworkVariable<bool> hasPassedP1 = new NetworkVariable<bool>();
    public NetworkVariable<bool> hasPassedP0 = new NetworkVariable<bool>();

    public NetworkVariable<byte> hasPriority = new NetworkVariable<byte>();

    /*
    0: P0
    1: P1
    2: No player/NA
    */

    public bool startOfPhase = true;

//_______________________________________________________________
    //Things to do with Lands

    //Has the Ap made a land drop (shared between players)
    public NetworkVariable<bool> hasPlayedLand = new NetworkVariable<bool>(false);


//_______________________________________________________________
    //Misc Items

    //Ap (Active player) stores who's turn it is
    public NetworkVariable<bool> Ap = new NetworkVariable<bool>();
    public byte _Ap; //Local Ap which is an byte rather than a bool

    public bool passingForRestOfTurnP1 = false;
    public bool passingForRestOfTurnP0 = false;

    //Tracks the current phase of the game
    public NetworkVariable<byte> phase = new NetworkVariable<byte>();

    public enum ActionStates {normal, choosingTargets, scrying};
    public ActionStates ActionState = ActionStates.normal;

    public Target globalTarget = new Target(255, 255, 255, 255, 255, 255);

    public bool[] selectZones = new bool[7] {false, false, false, false, false, false, false};
    public bool mustSelectSameOwner = false;

    public byte[,] upkeepDraws = new byte[2,9] {{0, 0, 0, 0, 0, 0, 0, 0, 0}, 
                                                {0, 0, 0, 0, 0, 0, 0, 0, 0}};
    

//_______________________________________________________________
    //Functions


    public void Shuffle () {

        Card tempCard;
        for (int i = 0; i < numCards[0][0]-2; i++) {
            //Swaps card i with a random card
            tempCard = GS[0][0, i];
            byte randomCard = (byte)UnityEngine.Random.Range(i, numCards[0][0]);
            GS[0][0, i] = GS[0][0, randomCard];
            GS[0][0, randomCard] = tempCard;
        }

        // Reset the indexInArray for each card in the deck
        for (byte i = 0; i < numCards[0][0]; i++) {
            GS[0][0, i].indexInArray = i;
            GS[0][0, i].knownTo[0] = false;
            GS[0][0, i].knownTo[1] = false;
        }
    }

    void DrawCard(bool player, int number = 1) {
        //Check for game loss from decking
        if (numCards[0][0] - number < 0) {
            //player losses the game
        }

        //Make sure that all of the cards could fit into the player's hand
        if (player) {
            if (numCards[1][1] + number > GLC.MAX_MAX_HAND_SIZE) {
                //Couldn't fit all of the cards into the player's hand
                Debug.Log("P1 Tried to draw more cards than MAX_MAX_HAND_SIZE");
                return;
            }
        }else {
            if (numCards[1][0] + number > GLC.MAX_MAX_HAND_SIZE) {
                //Couldn't fit all of the cards into the player's hand
                Debug.Log("P0 Tried to draw more cards than MAX_MAX_HAND_SIZE");
                return;
            }
        }

        //Get the cards to be drawn
        byte[] cardsToDraw = new byte[number];
        for (int i = 0; i < number; i++) {
            cardsToDraw[i] = GS[0][0, i].id;
        }

        //Remove cards from the deck and shift array elements up to fill the cards drawn.
        for (int i = 0; i < GLC.MAX_DECK_SIZE-number; i++) {
            GS[0][0, i] = GS[0][0, i+number];
            GS[0][0, i].indexInArray = (byte)i;
        }
        for (int i = GLC.MAX_DECK_SIZE-number; i < GLC.MAX_DECK_SIZE; i++) {
            GS[0][0, i] = nullCard;
        }
        //Correct the new decksize.
        numCards[0][0] -= (byte)number;


        //Add cards to the player's hand
        int cardNumToAdd = 0;
        if (player) {
            for (int i = numCards[1][1]; i < numCards[1][1] + number; i++) {
                    GS[1][1, i] = new Card(cardsToDraw[cardNumToAdd], (byte)i, 1, 1, GameObject.FindGameObjectsWithTag("Player")[0].GetComponent<PlayerScript>(), this);
                    //handP1[i].id = cardsToDraw[cardNumToAdd];
                    //Instantiate(handP1[i].physical, handP1Canvas.transform);
                    cardNumToAdd++;
            }
            numCards[1][1] += (byte)number;


            //UpdateP1Hand
            //handP1Canvas.GetComponent<DisplayHand>().UpdateHandP1Display();
        }else {
            for (int i = numCards[1][0]; i < numCards[1][0] + number; i++) {
                    GS[1][0, i] = new Card(cardsToDraw[cardNumToAdd], (byte)i, 1, 0, GameObject.FindGameObjectsWithTag("Player")[0].GetComponent<PlayerScript>(), this);
                    //handP0[i].id = cardsToDraw[cardNumToAdd];
                    //Add instantiate for HandP0
                    cardNumToAdd++;
            }
            numCards[1][0] += (byte)number;
            //UpdateP0Hand Here when made
        }
    }

    public void SetUpGameForClient() {
        for (int i = 0; i < GLC.MAX_DECK_SIZE; i++) {
            GS[0][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.MAX_MAX_HAND_SIZE; i++) {
            GS[1][1, i] = nullCard;
            GS[1][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxLands; i++) {
            GS[4][1, i] = nullCard;
            GS[4][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxCreatures; i++) {
            GS[3][1, i] = nullCard;
            GS[3][0, i] = nullCard;
        }


        for (int i = 0; i < GLC.maxStackSize; i++) {
            GS[2][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxGraveyard; i++) {
            GS[5][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxExile; i++) {
            GS[6][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxMovableCards; i++) {
            GS[7][0, i] = nullCard;
        }
    }

    public void SetUpGameForServer() 
    {
        hasPriority.Value = 2;
        phase.Value = 3;
        hasPassedP1.Value = false;
        hasPassedP0.Value = false;
        Ap.Value = true;
        PlayerScript PS = GameObject.FindGameObjectsWithTag("Player")[0].GetComponent<PlayerScript>();
        
        //Create each card

        //Assigns card prefabs to cardPrefabs
        //cardPrefabs[0] = GameObject.F
        //public Card[] cards = new Card[3];
        //Island
        //island = new(0, "island");
        //DanDan
        //dandan = new(1, "dandan");

        //Card[] cards = new Card[2]; This is done up top
        //sd.cards[0] = island;
        //sd.cards[1] = dandan;


        //Initialize lands
        for (int i = 0; i < GLC.maxLands; i++) {
            GS[4][1, i] = nullCard;
            GS[4][0, i] = nullCard;
        }

        //Initialize hand
        for (int i = 0; i < GLC.MAX_MAX_HAND_SIZE; i++) {
            GS[1][1, i] = nullCard;
            GS[1][0, i] = nullCard;
        }

        //Initialize the stack
        for (int i = 0; i < GLC.maxStackSize; i++) {
            GS[2][0, i] = nullCard;
        }

        //Initialize creatures
        for (int i = 0; i < GLC.maxCreatures; i++) {
            GS[3][1, i] = nullCard;
            GS[3][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxGraveyard; i++) {
            GS[5][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxExile; i++) {
            GS[6][0, i] = nullCard;
        }

        for (int i = 0; i < GLC.maxMovableCards; i++) {
            GS[7][0, i] = nullCard;
        }


        //Preset game for testing
        //Give Players 2 islands
        GS[4][0, 0] = new Card(0, 0, 4, 0, PS, this);
        GS[4][0, 1] = new Card(0, 1, 4, 0, PS, this);

        GS[4][1, 0] = new Card(0, 0, 4, 1, PS, this);
        GS[4][1, 1] = new Card(0, 1, 4, 1, PS, this);
        GS[4][1, 2] = new Card(0, 2, 4, 1, PS, this);
        GS[4][1, 3] = new Card(0, 3, 4, 1, PS, this);

        numCards[4][0] = 2;
        numCards[4][1] = 4;








        //Set each player's life total to 20
        lifeTotal[0] = 20;
        lifeTotal[1] = 20;

        //Add cards to the deck
        //Currently adds 40 islands and 20 DanDans and 20 MemoryLapses
        for (byte i = 0; i < 15; i++) {
            GS[0][0, i] = new Card(0, i, 0, 2, PS, this); //Island
        }
        for (byte i = 15; i < 25; i++) {
            GS[0][0, i] = new Card(1, i, 0, 2, PS, this); // Dandan
        }
        for (byte i = 25; i < 35; i++) {
            GS[0][0, i] = new Card(8, i, 0, 2, PS, this); // Fetid Pools
        }
        for (byte i = 35; i < 37; i++) {
            GS[0][0, i] = new Card(2, i, 0, 2, PS, this); // MemoryLapse
        }
        for (byte i = 37; i < 54; i++) {
            GS[0][0, i] = new Card(3, i, 0, 2, PS, this); // LatNam'sLegacy
        }
        for (byte i = 54; i < 56; i++) {
            GS[0][0, i] = new Card(6, i, 0, 2, PS, this); // Chemister's Insight
        }
        for (byte i = 56; i < 80; i++) {
            GS[0][0, i] = new Card(7, i, 0, 2, PS, this); // Tamiyo's Epiphany
        }
        for (byte i = 80; i < 60+20+0; i++) {
            GS[0][0, i] = nullCard;
            //sd.deck[i].uniqueId = (byte)i;
        }

        for (int i = 0; i < 80; i++) {
            if (i == -1) {
                GS[0][0, i].knownTo[0] = true;
                GS[0][0, i].knownTo[1] = true;
            }else {
                GS[0][0, i].knownTo[0] = false;
                GS[0][0, i].knownTo[1] = false;
            }
        }

        numCards[0][0] = 80;


        Shuffle();


        for (int i = 0; i < GLC.maxStackSize; i++) {
            GS[2][0, i] = nullCard;
        }

        DrawCard(true, 7);
        DrawCard(false, 7);
        
    }


}



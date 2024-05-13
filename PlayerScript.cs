using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

//using Mono.Cecil.Cil;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Scripting;



/*
This will run the game


*/

public class PlayerScript : NetworkBehaviour
{




    const int DECK = 0;                   
    const int HAND = 1;
    const int STACK = 2;
    const int CREATURES = 3;
    const int LANDS = 4;
    const int GRAVEYARD = 5;
    const int EXILE = 6;
    const int MOVABLE = 7;




    // Variable Declaration

    private DisplayHand[] displayHand = new DisplayHand[2];
    //private DisplayHand displayHandP0;
    //private DisplayPermanants displayPermanantsP1;
    //private DisplayPermanants displayPermanantsP0;
    private DisplayPermanants[] displayPermanants = new DisplayPermanants[2];

    private DisplayStack displayStack;

    private DisplayGraveyard displayGraveyard;

    private DisplayExile displayExile;

    private DisplayDeck displayDeck;
    public DisplayMovableCards displayMovable;

    private GameData gd;

    private SetNumValues setNumValues;





    //___________________________________________________________________________________________________________________________________________________
    // Client-Server Connection Set-up


    [ClientRpc]
    void ShowGamePlayClientRpc() {
        //GameObject.Find("Display Manager").GetComponent<DisplayManager>().showGamePlay(joinCode);
    }

    private void Initialize() 
    {
        //Create game data
        ///GameObject serverDataGO = GameObject.Find("GameData");
        gd = gameObject.GetComponent<GameData>(); //gd stands for gameData


        if (IsServer) gd.SetUpGameForServer();
        else gd.SetUpGameForClient();
        

        //if (IsHost && IsOwner) gameObject.name = "Player1";
        //else gameObject.name = "Player2";
        if (IsHost) {
            gameObject.name = "Host";
            
        }else {
            gameObject.name = "Client";
        }

        if (IsClient) {
            if (IsHost) {
                displayHand[1] = GameObject.Find("YourHandBackground").GetComponent<DisplayHand>();
                displayHand[0] = GameObject.Find("OpponentHandBackground").GetComponent<DisplayHand>();
                displayPermanants[1] = GameObject.Find("YourBattlefieldBackground").GetComponent<DisplayPermanants>();
                displayPermanants[0] = GameObject.Find("OpponentBattlefieldBackground").GetComponent<DisplayPermanants>();
            }else {
                displayHand[1] = GameObject.Find("OpponentHandBackground").GetComponent<DisplayHand>();
                displayHand[0] = GameObject.Find("YourHandBackground").GetComponent<DisplayHand>();
                displayPermanants[1] = GameObject.Find("OpponentBattlefieldBackground").GetComponent<DisplayPermanants>();
                displayPermanants[0] = GameObject.Find("YourBattlefieldBackground").GetComponent<DisplayPermanants>();
            }
            displayStack = GameObject.Find("StackBackground").GetComponent<DisplayStack>();
            setNumValues = GameObject.Find("InfoCanvas").GetComponent<SetNumValues>();
            displayGraveyard = GameObject.Find("GraveyardBackground").GetComponent<DisplayGraveyard>();
            displayExile = GameObject.Find("ExileBackground").GetComponent<DisplayExile>();
            displayDeck = GameObject.Find("DeckBackground").GetComponent<DisplayDeck>();
            displayMovable = GameObject.Find("MovableDisplayBackground").GetComponent<DisplayMovableCards>();
        }

        if (IsClient) {

            SyncDeckServerRpc();
            SyncHandServerRpc(true);
            SyncHandServerRpc(false);
            SyncLifeServerRpc(true);
            SyncLifeServerRpc(false);
            SyncCreaturesServerRpc(true);
            SyncCreaturesServerRpc(false);
            SyncLandsServerRpc(true);
            SyncLandsServerRpc(false);
            SyncGraveyardServerRpc();

            setNumValues.SetInstruction("Clear");
            

        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }





    //______________________________________________________________________________________________________________________________
    // Functions


    bool byteToBool (byte num) {
        if (num == 1) return true;
        else return false;
    }

    byte boolToByte (bool _bool) {
        if (_bool) return 1;
        else return 0;
    }


    // Syncing Server-Client Data

    public bool GetIsHost () {
        return IsHost;
    }

    public byte getCardId (byte zone, byte owner, byte index) {
        if (zone == 0 || zone == 2 || zone == 5 || zone == 6 || zone == 7) {
            return gd.GS[zone][0, index].id;
        }else {
            return gd.GS[zone][owner, index].id;
        }
    }


    [ClientRpc]
    void SetInstructionClientRpc (bool player, FixedString32Bytes textId) {
        if (player == IsHost) {
            setNumValues.SetInstruction(textId);
        }
    }


    void Shuffle () {
        if (!IsHost) return;
        gd.Shuffle();
        SyncDeckServerSide();
    }

    [ClientRpc]
    void SyncDeckClientRpc (byte[] serverDeck, bool[] knownToP1, bool[] knownToP0) {
        // serverDeck only passes in the actual cards. No nullcards are passed to save bandwidth

        gd.numCards[DECK][0] = (byte)serverDeck.Length;
        
        //Checks all the actual Cards
        for (byte i = 0; i < gd.numCards[DECK][0]; i++) {
            if (gd.GS[DECK][0, i].id != serverDeck[i]) {
                gd.GS[DECK][0, i] = new Card(serverDeck[i], i, DECK, 2, this, gd);
            }

            gd.GS[DECK][0, i].knownTo[1] = knownToP1[i];  
            gd.GS[DECK][0, i].knownTo[0] = knownToP0[i];       
            }

        //Checks what should be null cards
        for (int i = gd.numCards[DECK][0]; i < GLC.MAX_DECK_SIZE; i++) {
            //if (gd.GS[DECK][0, i].id != 255) {
                gd.GS[DECK][0, i] = new Card(255, 255, DECK, 255, null, null); //null card
            //}
        }

        Card[] tempDeck = new Card[gd.numCards[DECK][0]];
        for (int i = 0; i < tempDeck.Length; i++) {
            tempDeck[i] = gd.GS[DECK][0, i];
        }

        displayDeck.UpdateDeckDisplay(tempDeck);
    }

    void SyncDeckServerSide () {
        byte[] byteDeck = new byte[gd.numCards[DECK][0]];
        bool[] knownToP1 = new bool[gd.numCards[DECK][0]];
        bool[] knownToP0 = new bool[gd.numCards[DECK][0]];
        for (int i = 0; i < gd.numCards[DECK][0]; i++) {
            if (gd.GS[DECK][0, i].id == 255) break; //this line should be unnessecary
            else {
                byteDeck[i] = gd.GS[DECK][0, i].id;
                knownToP1[i] = gd.GS[DECK][0, i].knownTo[1];
                knownToP0[i] = gd.GS[DECK][0, i].knownTo[0];
            }
        }

        SyncDeckClientRpc(byteDeck, knownToP1, knownToP0);
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncDeckServerRpc () {
        SyncDeckServerSide();
    }





    [ClientRpc]
    void SyncHandClientRpc (bool _player, byte[] serverHand) {
        byte player = boolToByte(_player);
        

        // serverHand only passes in the actual cards. No nullcards are passed to save bandwidth

        gd.numCards[HAND][player] = (byte)serverHand.Length;

        //Checks all the actual Cards
        for (byte i = 0; i < gd.numCards[HAND][player]; i++) {
            if (gd.GS[HAND][player, i].id != serverHand[i]) {
                gd.GS[HAND][player, i] = new Card(serverHand[i], i, HAND, player, this, gd);
            }
        }

        //Checks what should be null cards
        for (byte i = gd.numCards[HAND][player]; i < GLC.MAX_MAX_HAND_SIZE; i++) {
            if (gd.GS[HAND][player, i].id != 255) {
                gd.GS[HAND][player, i] = new Card(255, i, HAND, player, this, gd);
            }
        }

        byte[] handIds = new byte[GLC.MAX_MAX_HAND_SIZE];
        for (int i = 0; i < GLC.MAX_MAX_HAND_SIZE; i++) {
            handIds[i] = gd.GS[HAND][player, i].id;
        }

        displayHand[player].UpdateHandDisplay(handIds);

    }

    void SyncHandServerSide (byte player) {
        byte[] byteHand  = new byte[gd.numCards[HAND][player]];
        for (int i = 0; i < gd.numCards[HAND][player]; i++) {
            if (gd.GS[HAND][player, i].id == 255) break; //this line should be unnessecary
            else byteHand[i] = gd.GS[HAND][player, i].id;
        }
        SyncHandClientRpc(byteToBool(player), byteHand);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncHandServerRpc (bool player) {
        SyncHandServerSide(boolToByte(player));
    }





    [ClientRpc]
    void SyncLifeClientRpc (bool player, int life) {
        setNumValues.SetLifeTxt(player, life);
    }

    void SyncLifeServerSide (byte player) {
        SyncLifeClientRpc(byteToBool(player), gd.lifeTotal[player]);
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncLifeServerRpc (bool player) {
        SyncLifeServerSide(boolToByte(player));
    }






    [ClientRpc]
    void SyncLandsClientRpc (bool _player, byte[] ids, bool[] untapped) {
        byte player = boolToByte(_player);
        
        gd.numCards[LANDS][player] = (byte)ids.Length;

        //Checks all the actual Cards
        for (byte i = 0; i < gd.numCards[LANDS][player]; i++) {
            if (gd.GS[LANDS][player, i].id != ids[i]) {
                gd.GS[LANDS][player, i] = new Card(ids[i], i, LANDS, player, this, gd);
            }

            gd.GS[LANDS][player, i].untapped = untapped[i];
        }

        //Checks what should be null cards
        for (int i = gd.numCards[LANDS][player]; i < GLC.maxLands; i++) {
            if (gd.GS[LANDS][player, i].id != 255) {
                gd.GS[LANDS][player, i] = new Card(255, 255, LANDS, player, this, gd);
            }
        }

        byte[] displayIds = new byte[GLC.maxLands];
        bool[] displayUntapped = new bool[GLC.maxLands];
        for (int i = 0; i < GLC.maxLands; i++) {
            displayIds[i] = gd.GS[LANDS][player, i].id;
            displayUntapped[i] = gd.GS[LANDS][player, i].untapped;
        }


        displayPermanants[player].UpdateLandsDisplay(displayIds, displayUntapped);

    }

    void SyncLandsServerSide (byte player) {
        if (!IsServer) return;

        byte[] landIds = new byte[gd.numCards[LANDS][player]];
        bool[] untapped = new bool[gd.numCards[LANDS][player]];

        for (byte i = 0; i < gd.numCards[LANDS][player]; i++) {
            landIds[i] = gd.GS[LANDS][player, i].id;
            untapped[i] = gd.GS[LANDS][player, i].untapped;
        }

        SyncLandsClientRpc(byteToBool(player), landIds, untapped);

    }

    [ServerRpc(RequireOwnership = false)]
    void SyncLandsServerRpc (bool player) {
        SyncLandsServerSide(boolToByte(player));
    }




    [ClientRpc]
    void SyncCreaturesClientRpc (bool _player, byte[] ids, bool[] untapped, bool[] isSummoningSick, bool[] isInCombat) {
        byte player = boolToByte(_player);

        gd.numCards[CREATURES][player] = (byte)ids.Length;

        //Checks all the actual Cards
        for (byte i = 0; i < gd.numCards[CREATURES][player]; i++) {
            if (gd.GS[CREATURES][player, i].id != ids[i]) {
                gd.GS[CREATURES][player, i] = new Card(ids[i], i, CREATURES, player, this, gd);
            }

            gd.GS[CREATURES][player, i].untapped = untapped[i];
            gd.GS[CREATURES][player, i].isSummoningSick = isSummoningSick[i];
            gd.GS[CREATURES][player, i].isAttackOrBlocking = isInCombat[i];
        }

        //Checks what should be null cards
        for (int i = gd.numCards[CREATURES][player]; i < GLC.maxCreatures; i++) {
            if (gd.GS[CREATURES][player, i].id != 255) {
                gd.GS[CREATURES][player, i] = new Card(255, 255, CREATURES, player, this, gd);
            }
        }

        byte[] displayIds = new byte[GLC.maxCreatures];
        bool[] displayUntapped = new bool[GLC.maxCreatures];
        bool[] displaySummoningSick = new bool[GLC.maxCreatures];
        bool[] displayIsInCombat = new bool[GLC.maxCreatures];
        for (int i = 0; i < GLC.maxCreatures; i++) {
            displayIds[i] = gd.GS[CREATURES][player, i].id;
            displayUntapped[i] = gd.GS[CREATURES][player, i].untapped;
            displaySummoningSick[i] = gd.GS[CREATURES][player, i].untapped;
            displayIsInCombat[i] = gd.GS[CREATURES][player, i].isAttackOrBlocking;
        }

        displayPermanants[player].UpdateCreaturesDisplay(displayIds, displayUntapped, displaySummoningSick, displayIsInCombat);
    }

    void SyncCreaturesServerSide (byte player) {
        if (!IsServer) return;

        byte[] creatureIds = new byte[gd.numCards[CREATURES][player]];
        bool[] untapped = new bool[gd.numCards[CREATURES][player]];
        bool[] isSummoningSick = new bool[gd.numCards[CREATURES][player]];
        bool[] isInCombat = new bool[gd.numCards[CREATURES][player]];

        for (byte i = 0; i < gd.numCards[CREATURES][player]; i++) {
            creatureIds[i] = gd.GS[CREATURES][player, i].id;
            untapped[i] = gd.GS[CREATURES][player, i].untapped;
            isSummoningSick[i] = gd.GS[CREATURES][player, i].isSummoningSick;
            isInCombat[i] = gd.GS[CREATURES][player, i].isAttackOrBlocking;
        }

        SyncCreaturesClientRpc(byteToBool(player), creatureIds, untapped, isSummoningSick, isInCombat);

    }
    [ServerRpc(RequireOwnership = false)]
    public void SyncCreaturesServerRpc (bool player) {
        SyncCreaturesServerSide(boolToByte(player));
    }



    [ClientRpc]
    void SyncStackClientRpc(byte[] ids, byte[] owners, byte[] targetZones, byte[] targetOwners, byte[] targetIndices) {
        gd.numCards[STACK][0] = (byte)ids.Length;
        for (byte i = 0; i < gd.numCards[STACK][0]; i++) {
            if (gd.GS[STACK][0, i].id != ids[i]) {
                gd.GS[STACK][0, i] = new Card(ids[i], i, STACK, owners[i], this, gd); //The stack needs to sync the owners of the cards too and any targets
                gd.GS[STACK][0, i].target = new Target(STACK, i, owners[i], targetZones[i], targetIndices[i], targetOwners[i]);
            }
        }

        for (byte i = gd.numCards[STACK][0]; i < GLC.maxStackSize; i++) {
            if (gd.GS[STACK][0, i].id != 255) {
                gd.GS[STACK][0, i] = new Card(255, 255, STACK, 2, this, gd);
            }
        }

        Card[] tempStack = new Card[gd.numCards[STACK][0]];
        for (int i = 0; i < tempStack.Length; i++) {
            tempStack[i] = gd.GS[STACK][0, i];
        }

        displayStack.UpdateStackDisplay(tempStack);
    }

    void SyncStackServerSide() {
        byte[] byteStack = new byte[gd.numCards[STACK][0]];
        byte[] byteOwner = new byte[gd.numCards[STACK][0]];
        byte[] byteTargetZone = new byte[gd.numCards[STACK][0]];
        byte[] byteTargetOwner = new byte[gd.numCards[STACK][0]];
        byte [] byteTargetIndex = new byte[gd.numCards[STACK][0]];

        for (byte i = 0; i < gd.numCards[STACK][0]; i++) {
            byteStack[i] = gd.GS[STACK][0, i].id;
            byteOwner[i] = boolToByte(gd.GS[STACK][0, i].owner);
            byteTargetZone[i] = gd.GS[STACK][0, i].target.targetZone;
            byteTargetOwner[i] = gd.GS[STACK][0, i].target.targetOwner;
            byteTargetIndex[i] = gd.GS[STACK][0, i].target.targetIndex;
            Debug.Log("OnStack[" + i + "]: " + gd.GS[STACK][0, i].id);

        }

        SyncStackClientRpc(byteStack, byteOwner, byteTargetZone, byteTargetOwner, byteTargetIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncStackServerRpc () {
        SyncStackServerSide();
    }




    [ClientRpc]
    void SyncGraveyardClientRpc(byte[] ids) {
        gd.numCards[GRAVEYARD][0] = (byte)ids.Length;
        for (byte i = 0; i < gd.numCards[GRAVEYARD][0]; i++) {
            if (gd.GS[GRAVEYARD][0, i].id != ids[i]) {
                gd.GS[GRAVEYARD][0, i] = new Card(ids[i], i, GRAVEYARD , 2, this, gd);
            }
        }

        for (byte i = gd.numCards[GRAVEYARD][0]; i < GLC.maxGraveyard; i++) {
            if (gd.GS[GRAVEYARD][0, i].id != 255) {
                gd.GS[GRAVEYARD][0, i] = new Card(255, 255, GRAVEYARD, 2, this, gd);
            }
        }

        byte[] graveyardIds = new byte[GLC.maxGraveyard];
        for (int i = 0; i < GLC.maxGraveyard; i++) {
            graveyardIds[i] = gd.GS[GRAVEYARD][0, i].id;
        }

        displayGraveyard.UpdateGraveyardDisplay(graveyardIds);
    }


    void SyncGraveyardServerSide() {
        byte[] byteGraveyard = new byte[gd.numCards[GRAVEYARD][0]];
        for (byte i = 0; i < gd.numCards[GRAVEYARD][0]; i++) {
            byteGraveyard[i] = gd.GS[GRAVEYARD][0, i].id;
        }

        SyncGraveyardClientRpc(byteGraveyard);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncGraveyardServerRpc () {
        SyncGraveyardServerSide();
    }



    [ClientRpc]
    void SyncExileClientRpc(byte[] ids) {
        gd.numCards[EXILE][0] = (byte)ids.Length;
        for (byte i = 0; i < gd.numCards[EXILE][0]; i++) {
            if (gd.GS[EXILE][0, i].id != ids[i]) {
                gd.GS[EXILE][0, i] = new Card(ids[i], i, EXILE , 2, this, gd);
            }
        }

        for (byte i = gd.numCards[EXILE][0]; i < GLC.maxExile; i++) {
            if (gd.GS[EXILE][0, i].id != 255) {
                gd.GS[EXILE][0, i] = new Card(255, 255, EXILE, 2, this, gd);
            }
        }

        byte[] exileIds = new byte[GLC.maxExile];
        for (int i = 0; i < GLC.maxExile; i++) {
            exileIds[i] = gd.GS[EXILE][0, i].id;
        }

        displayExile.UpdateExileDisplay(exileIds);
    }

    void SyncExileServerSide() {
        byte[] byteExile = new byte[gd.numCards[EXILE][0]];
        for (byte i = 0; i < gd.numCards[EXILE][0]; i++) {
            byteExile[i] = gd.GS[EXILE][0, i].id;
        }

        SyncExileClientRpc(byteExile);
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncExileServerRpc () {
        SyncExileServerSide();
    }




    [ClientRpc]
    void SyncMovableClientRpc(bool player, byte[] ids) {
        if (player == IsHost) {
            gd.numCards[MOVABLE][0] = (byte)ids.Length;
            for (byte i = 0; i < gd.numCards[MOVABLE][0]; i++) {
                if (gd.GS[MOVABLE][0, i].id != ids[i]) {
                    gd.GS[MOVABLE][0, i] = new Card(ids[i], i, MOVABLE , 2, this, gd);
                }
            }

            for (byte i = gd.numCards[MOVABLE][0]; i < GLC.maxMovableCards; i++) {
                if (gd.GS[MOVABLE][0, i].id != 255) {
                    gd.GS[MOVABLE][0, i] = new Card(255, 255, MOVABLE, 2, this, gd);
                }
            }

            Card[] movableCards = new Card[GLC.maxMovableCards];
            for (int i = 0; i < GLC.maxMovableCards; i++) {
                movableCards[i] = gd.GS[MOVABLE][0, i];
                movableCards[i].zone = MOVABLE;
                movableCards[i].indexInArray = (byte)i;
            }

            displayMovable.UpdateMovableDisplay(movableCards);
        }
    }

    void SyncMovableServerSide(bool player) {
        Debug.Log("numCards[MOVABLE]: " + gd.numCards[MOVABLE][0]);
        byte[] byteMovable = new byte[gd.numCards[MOVABLE][0]];
        for (byte i = 0; i < gd.numCards[MOVABLE][0]; i++) {
            byteMovable[i] = gd.GS[MOVABLE][0, i].id;
        }

        SyncMovableClientRpc(player, byteMovable);
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncMovableServerRpc (bool player) {
        SyncMovableServerSide(player);
    }















    //Game Functions (used to actually run the game)

    [ServerRpc(RequireOwnership = false)]
    public void AddCardToServerRpc (byte zone, byte zoneOwner, byte index = 255, byte id = 255, byte cardOwner = 255) {
        AddCardTo(zone, zoneOwner, index, id:id, cardOwner:cardOwner);
    }
    public void AddCardTo (byte zone, byte zoneOwner, byte index = 255, Card card = null, byte id = 255, byte cardOwner = 255) {
        if (cardOwner == 255) {
            cardOwner = zoneOwner;
        }
        if (zoneOwner == 2) {
            zoneOwner = 0;
        }
        
        if (index != 255) {
            //Shift Cards Down
            for (int i = gd.numCards[zone][zoneOwner]; i > index; i--) {
                gd.GS[zone][zoneOwner, i] = gd.GS[zone][zoneOwner, i-1];
                gd.GS[zone][zoneOwner, i].indexInArray = (byte)i;
            }
        }else {

            index = gd.numCards[zone][zoneOwner];
        }



        if (id != 255) {
            gd.GS[zone][zoneOwner, index] = new Card (id, index, zone, cardOwner, this, gd);
        }else {
            gd.GS[zone][zoneOwner, index] = card;
            gd.GS[zone][zoneOwner, index].zone = zone;
            gd.GS[zone][zoneOwner, index].indexInArray = index;
        }

        gd.numCards[zone][zoneOwner]++;


    

    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveCardFromServerRpc (byte zone, byte owner, byte index) {
        RemoveCardFrom(zone, owner, index);
    }
    public void RemoveCardFrom (byte zone, byte owner, byte index) {
        if (owner == 2) {
            owner = 0;
        }
        if (gd.GS[zone][owner, index].id == 255) {
            Debug.Log("Error: Tried to remove a nonexistant card");
            return;
        }

        gd.GS[zone][owner, index] = gd.nullCard;

        for (byte i = index; i < gd.numCards[zone][owner] - 1; i++) {
            gd.GS[zone][owner, i] = gd.GS[zone][owner, i+1];
            gd.GS[zone][owner, i].indexInArray = i;
        }

        
        gd.numCards[zone][owner]--;

    }



    [ServerRpc(RequireOwnership = false)]
    public void DrawCardServerRpc (byte player, byte number = 1) {
        DrawCardServerSide(player, number);
    }
    void DrawCardServerSide(byte player, int number = 1) {
        if (!IsServer) return;
        //Check for game loss from decking
        if (gd.numCards[DECK][0] - number < 0) {
            //player losses the game
        }

        
        //Make sure that all of the cards could fit into the player's hand
        if (gd.numCards[HAND][player] + number > GLC.MAX_MAX_HAND_SIZE) {
            //Couldn't fit all of the cards into the player's hand
            Debug.Log(player + " tried to draw more cards than MAX_MAX_HAND_SIZE");
            return;
        }

        
        

        //Get the cards to be drawn
        byte[] cardsToDraw = new byte[number];
        for (int i = 0; i < number; i++) {
            cardsToDraw[i] = gd.GS[DECK][0, i].id;
        }

        //Remove cards from the deck and shift array elements up to fill the cards drawn.
        for (int i = 0; i < GLC.MAX_DECK_SIZE-number; i++) {
            gd.GS[DECK][0, i] = gd.GS[DECK][0, i+number];
            gd.GS[DECK][0, i].indexInArray = (byte)i;
        }
        for (int i = GLC.MAX_DECK_SIZE-number; i < GLC.MAX_DECK_SIZE; i++) {
            gd.GS[DECK][0, i] = gd.nullCard;
        }
        //Correct the new decksize.
        gd.numCards[DECK][0] -= (byte)number;


        //Add cards to the player's hand
        int cardNumToAdd = 0;

        for (int i = gd.numCards[HAND][player]; i < gd.numCards[HAND][player] + number; i++) {
                gd.GS[HAND][player, i] = new Card(cardsToDraw[cardNumToAdd], (byte)i, HAND, player, this, gd);
                cardNumToAdd++;
        }
        gd.numCards[HAND][player] += (byte)number;

        SyncDeckServerSide();

        SyncHandServerSide(player);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PutInDeckServerRpc (byte id, int index, bool[] knownTo = null) {
        PutInDeck(id, index, knownTo);
    }  
    public void PutInDeck (byte id, int index, bool[] knownTo = null) {
        if (index < 0) {
            //if negitive index, put card on the bottom
            if (index != -1) {
                Debug.Log("Too complicated, can only put card on bottom of deck");
                return;
            }
            gd.GS[DECK][0, gd.numCards[DECK][0]] = new Card(id, gd.numCards[DECK][0], DECK, 2, this, gd);
            if (knownTo != null) {
                gd.GS[DECK][0, gd.numCards[DECK][0]].knownTo = knownTo;
            }
            gd.numCards[DECK][0]++;

            

        }else {
            for (byte i = (byte)(GLC.MAX_DECK_SIZE - 1); i > index; i--) {
                gd.GS[DECK][0, i] = gd.GS[DECK][0, i-1];
                gd.GS[DECK][0, i].indexInArray = i;
            }

            gd.GS[DECK][0, index] = new Card(id, (byte)index, DECK, 2, this, gd);
            if (knownTo != null) {
                gd.GS[DECK][0, index].knownTo = knownTo;
            }
            
            gd.numCards[DECK][0]++;
        }


        SyncDeckServerSide();
    }
    
    public void RemoveFromHand(byte player, byte index) {

        RemoveFromHandServerRpc(byteToBool(player), index);
    }

    [ServerRpc(RequireOwnership = false)]
    void RemoveFromHandServerRpc (bool player, byte index) {
        RemoveFromHandServerSide(boolToByte(player), index);
    }

    void RemoveFromHandServerSide (byte player, byte index) {
        if (!IsServer) return;

        gd.GS[HAND][player, index] = gd.nullCard;
        for (int i = index; i < GLC.MAX_MAX_HAND_SIZE - 1; i++) {
            gd.GS[HAND][player, i] = gd.GS[HAND][player, i+1];
            gd.GS[HAND][player, i].indexInArray = (byte)i;
        }

        gd.GS[HAND][player, GLC.MAX_MAX_HAND_SIZE-1] = gd.nullCard;

        gd.numCards[HAND][player]--;

        SyncHandServerSide(player);
    }
    

    public bool CanPayMana(byte player, int _colorless, int _blue) {
        //Returns if player has enough mana available
        int _manaAvailable = 0;

        for (int i = 0; i < gd.numCards[LANDS][player]; i++) {
            if (gd.GS[LANDS][player, i].untapped) {
                _manaAvailable++;
            }
        }


        if (_manaAvailable >= _colorless + _blue) {
            return true;
        }//Else:
        return false;
    }


    bool AutotapServerSide(byte player, int _colorless, int _blue) {
        if (!IsServer) return false;

        //Tap colorless lands first if possible
        //Then tap blue sources

        //Check to make sure that the player can cast the spell
        if (!CanPayMana(player, _colorless, _blue)) {
            //Player can't afford to play the spell
            return false;
        }

        //Guarenteed to have enough mana to cast spell

        //Right now we only have blue sources so just tap the first untapped lands found
        int _manaPaid = 0;

        for (int i = 0; i < gd.numCards[LANDS][player]; i++) {
            //Check to see if all costs have been paid
            //See if land is untapped
            if (_manaPaid < (_colorless + _blue) && gd.GS[LANDS][player, i].untapped) {

                //Tap land for mana
                _manaPaid++;
                gd.GS[LANDS][player, i].untapped = false;
            }
        }

        SyncLandsServerSide(player);


        return true;
    }



    public void AttemptToPlay(byte zone, byte player, byte index, byte targetZone = 255, byte targetOwner = 255, byte targetIndex = 255) {
        if (IsClient) AttemptToPlayServerRpc (zone, player, index, targetZone, targetOwner, targetIndex);
    }



    [ServerRpc(RequireOwnership = false)]
    void AttemptToPlayServerRpc (byte zone, byte player, byte index, byte targetZone = 255, byte targetOwner = 255, byte targetIndex = 255) {
        if (!IsServer) return;

        bool wasSuccessful = false;

        //Attempts to play this card if possible
        //If type is land put it into play if land drop has not been played
        Debug.Log("Attempt To Play: " + zone + ", " + player + ", " + index);
        byte id;
        if (zone == 2 || zone == 5 || zone == 6) {
            id = gd.GS[zone][0, index].id;
        }else {
            id = gd.GS[zone][player, index].id;
        }
        if (id == 0) {
            //Card is a land

            //Check if a land has already been played for the turn
            //Check if it is the active player's land
            //Check if it is a main phase
            if (!gd.hasPlayedLand.Value && gd.numCards[STACK][0] == 0 && gd.hasPriority.Value == player && player == gd._Ap && (gd.phase.Value == 3 || gd.phase.Value == 9)) {
                //Play the land
                
                //AddToPlayServerRpc(player, id);
                /*
                gd.GS[LANDS][player, gd.numCards[LANDS][player]] = new Card(id, gd.numCards[LANDS][player], LANDS, player, this, gd);
                gd.numCards[LANDS][player]++;
                */
                AddCardTo(LANDS, player, id:id);
                
                gd.hasPlayedLand.Value = true;

                RemoveFromHandServerSide(player, index);

                //SyncHandServerSide(player);
                SyncLandsServerSide(player);

                wasSuccessful = true;
            }
            //Else don't do anything because the player has already played a land for the turn
        }
        else if(id == 1) {
            //Play a Dandan
            //Check for Sorcery speed
            if (gd.numCards[STACK][0] == 0 && player == gd._Ap && gd.hasPriority.Value == player && (gd.phase.Value == 3 || gd.phase.Value == 9)) {
                if (AutotapServerSide(player, 0, 2)) {
                    //Could play card and have autotapped the lands to cast the card
                    //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(id, gd.numCards[STACK][0], STACK, player, this, gd);
                    //gd.GS[STACK][0, gd.numCards[STACK][0]].owner = byteToBool(player);
                    //gd.numCards[STACK][0]++;

                    AddCardTo(STACK, 2, id:id, cardOwner:player);

                    SyncStackServerSide();

                    //Remove from hand
                    //RemoveFromHandServerSide(player, index);
                    RemoveCardFrom(HAND, player, index);
                    SyncHandServerSide(player);

                    wasSuccessful = true;
                }else {
                    Debug.Log("Not enough mana to play Dandan");
                }
            }else {
                //Could not play card
            }
        }
        else if (id == 2) {
            //Play a MemoryLapse
            //Check for priority, and targets
            if (player == gd.hasPriority.Value && gd.numCards[STACK][0] != 0 && CanPayMana(player, 1, 1)) {
                if (targetZone != 255) {
                    //Play MemoryLapse with the choosen target 
                    AutotapServerSide(player, 1, 1);
                    //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(id, gd.numCards[STACK][0], STACK, player, this, gd);
                    //gd.GS[STACK][0, gd.numCards[STACK][0]].owner = byteToBool(player);  I think that this line is unnessecary
                    AddCardTo(STACK, 2, id:id, cardOwner:player);
                    gd.GS[STACK][0, gd.numCards[STACK][0]-1].target = new Target(STACK, (byte)(gd.numCards[STACK][0]-1), player, targetZone, targetIndex, targetOwner);
                    //gd.numCards[STACK][0]++;
                    SyncStackServerSide();
                    //RemoveFromHandServerSide(player, index);
                    RemoveCardFrom(HAND, player, index);
                    wasSuccessful = true;
                }

            }
        }else if (id == 3) {
            //Lat-Nam's Legacy
            //Just move onto stack, choose shuffled in card on resolution
            if (player == gd.hasPriority.Value && CanPayMana(player, 1, 1)) {
                AutotapServerSide(player, 1, 1);
                
                //RemoveFromHandServerSide(player, index);
                RemoveCardFrom(HAND, player, index);
                
                //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(id, gd.numCards[STACK][0], STACK, player, this, gd);
                AddCardTo(STACK, 2, id:id, cardOwner:player);

                //gd.numCards[STACK][0]++;
                SyncStackServerSide();
                SyncHandServerSide(player);
        
                wasSuccessful = true;
            }
        }else if (id == 4) {
            Debug.Log("Attempted to play \"Draw a Card Trigger\"");
        }
        else if (id == 5) {
            Debug.Log("Attempted to play \"Draw two Cards Trigger\"");


        }else if (id == 6) {
            //Chemister's Insight
            if (zone == HAND) {
                //Add to stack, pay mana cost
                if (player == gd.hasPriority.Value && CanPayMana(player, 3, 1)) {
                    AutotapServerSide(player, 3, 1);
                    
                    //RemoveFromHandServerSide(player, index);
                    RemoveCardFrom(HAND, player, index);
                    
                    //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(id, gd.numCards[STACK][0], STACK, player, this, gd);
                    AddCardTo(STACK, 2, id:id, cardOwner:player);
                    
                    //gd.numCards[STACK][0]++;
                    SyncStackServerSide();
                    SyncHandServerSide(player);
            
                    wasSuccessful = true;
                }


            }else if (zone == GRAVEYARD) {
                //Chemister's Insight
                //Discard targeted card from hand
                //Add to stack, remove from graveyard, pay mana cost
                if (targetZone == 255 || targetOwner == 255 || targetIndex == 255) {
                    Debug.Log("Tried to cast Chemister's Insight from graveyard with no targets");
                    return;
                }

                if (targetZone != HAND) {
                    Debug.Log("Tried to cast Chemister's Insight discarding a card not in hand");
                    return;
                }

                if (targetOwner != player) {
                    Debug.Log("Tried to cast Chemister's Insight discarding a card not owned by caster");
                    return;
                }

                if (player == gd.hasPriority.Value && CanPayMana(player, 3, 1)) {

                    AutotapServerSide(player, 3, 1);

                    //Discarding the targeted Card
                    //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = new Card(gd.GS[HAND][targetOwner, targetIndex].id, gd.numCards[GRAVEYARD][0], GRAVEYARD, 2, this, gd);
                    //gd.numCards[GRAVEYARD][0]++;
                    AddCardTo(GRAVEYARD, 2, id:gd.GS[HAND][targetOwner, targetIndex].id);

                    /*
                    gd.GS[HAND][targetOwner, targetIndex] = gd.nullCard;
                    for (int i = targetIndex; i < GLC.MAX_MAX_HAND_SIZE-1; i++) {
                        gd.GS[HAND][targetOwner, i] = gd.GS[HAND][targetOwner, i+1];
                    }

                    gd.GS[HAND][targetOwner, GLC.MAX_MAX_HAND_SIZE-1] = gd.nullCard;
                    gd.numCards[HAND][targetOwner]--;
                    */

                    RemoveCardFrom(HAND, targetOwner, targetIndex);


                    //Add To Stack
                    //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(6, index, STACK, player, this, gd);
                    AddCardTo(STACK, 2, id:6, cardOwner:player);
                    gd.GS[STACK][0, gd.numCards[STACK][0]-1].exileOnResolution = true;
                    //gd.numCards[STACK][0]++;
                    
                    //Remove from Graveyard
                    RemoveCardFrom(GRAVEYARD, 2, index);
                    /*
                    gd.GS[GRAVEYARD][0, index] = gd.nullCard;
                    for (int i = index; i < GLC.maxGraveyard-1; i++) {
                        gd.GS[GRAVEYARD][0, i] = gd.GS[GRAVEYARD][0, i+1];
                        gd.GS[GRAVEYARD][0, i].indexInArray = (byte)i;
                    }
                    gd.GS[GRAVEYARD][0, GLC.maxGraveyard-1] = gd.nullCard;
                    gd.numCards[GRAVEYARD][0]--;
                    */


                    SyncGraveyardServerSide();
                    SyncHandServerSide(targetOwner);
                    SyncStackServerSide();
                    wasSuccessful = true;
                }


            }else {
                Debug.Log("Tried to play Chemister's Insight from somewhere other than hand or graveyard");
            }
        }else if (id == 7) {
            if (gd.GS[zone][player, index].canCast()) {
                AutotapServerSide(player, 3, 1);
                //Tamiyo's Epiphany
                //gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(7, gd.numCards[STACK][0], STACK, player, this, gd);
                //gd.numCards[STACK][0]++;
                AddCardTo(STACK, 2, id:7, cardOwner:player);
                SyncStackServerSide();

                //RemoveFromHand(player, index);
                RemoveCardFrom(HAND, player, index);
                SyncHandServerSide(player);
            }
        }if (id == 8) {
            //Card is a Fetid Pools

            //Check if a land has already been played for the turn
            //Check if it is the active player's land
            //Check if it is a main phase
            if (!gd.hasPlayedLand.Value && gd.numCards[STACK][0] == 0 && gd.hasPriority.Value == player && player == gd._Ap && (gd.phase.Value == 3 || gd.phase.Value == 9)) {
                //Play the land
                
                //AddToPlayServerRpc(player, id);
                /*
                gd.GS[LANDS][player, gd.numCards[LANDS][player]] = new Card(id, gd.numCards[LANDS][player], LANDS, player, this, gd);
                gd.numCards[LANDS][player]++;
                */
                AddCardTo(LANDS, player, id:id);
                
                gd.hasPlayedLand.Value = true;

                RemoveFromHandServerSide(player, index);

                //SyncHandServerSide(player);
                SyncLandsServerSide(player);

                wasSuccessful = true;
            }
            //Else don't do anything because the player has already played a land for the turn
        }

        if (wasSuccessful) {
            //Reset has passed
            gd.hasPassedP0.Value = false;
            gd.hasPassedP1.Value = false;
        }

    }





    [ServerRpc(RequireOwnership = false)]
    void AddToPlayServerRpc(bool _player, byte id) {
        byte player = boolToByte(_player);

        if (id == 1) {
            // Is Dandan
            gd.GS[CREATURES][player, gd.numCards[CREATURES][player]] = new Card(id, gd.numCards[CREATURES][player], CREATURES, player, this, gd);
            gd.numCards[CREATURES][player]++;

            SyncCreaturesServerSide(player);
        }else {
            // Is Land
            gd.GS[LANDS][player, gd.numCards[LANDS][player]] = new Card(id, gd.numCards[LANDS][player], CREATURES, player, this, gd);
            gd.numCards[LANDS][player]++;

            SyncLandsServerSide(player);
        }

    }



    [ServerRpc(RequireOwnership = false)]
    public void SwapAttackingStatusServerRpc(bool _player, byte index) {
        
        byte player = boolToByte(_player);

        gd.GS[CREATURES][player, index].isAttackOrBlocking = !gd.GS[CREATURES][player, index].isAttackOrBlocking;
        gd.GS[CREATURES][player, index].untapped = !gd.GS[CREATURES][player, index].isAttackOrBlocking;

        SyncCreaturesServerSide(player);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SwapBlockingStatusServerRpc(bool _player, byte id) {
        byte player = boolToByte(_player);

        gd.GS[CREATURES][player, id].isAttackOrBlocking = !gd.GS[CREATURES][player, id].isAttackOrBlocking;
        //gd.GS[CREATURES][player, id].untapped = !gd.GS[CREATURES][player, id].isAttackOrBlocking;

        SyncCreaturesServerSide(player);
    }



    public void ConfirmBtnClickServerSide(bool player) {
        Debug.Log("Confirm Button Clicked: " + player);
        if (gd.phase.Value == 5 && gd.hasPriority.Value == 2 && player == gd.Ap.Value) {
            //Attacks
            gd.hasPriority.Value = gd._Ap;
            SetInstructionClientRpc(gd.Ap.Value, "Clear");
        }else if (gd.phase.Value == 6 && gd.hasPriority.Value == 2 && player != gd.Ap.Value) {
            //Blocks
            gd.hasPriority.Value = gd._Ap;
            SetInstructionClientRpc(!gd.Ap.Value, "Clear");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmBtnClickServerRpc(bool player) {
        ConfirmBtnClickServerSide(player);
    }

    [ClientRpc]
    public void SetActionStateClientRpc (bool player, byte newState, bool[] _selectZones = null, byte _sourceZone = 255, byte _sourceOwner = 255,  byte _sourceIndex = 255, bool _mustSelectSameOwner = false) {
        if (player == IsHost) {
            gd.ActionState = (GameData.ActionStates)newState;
            if (_selectZones != null) gd.selectZones = _selectZones;
            if (_sourceZone != 255) gd.globalTarget = new Target(_sourceZone, _sourceIndex, _sourceOwner, 255, 255, 255);
            gd.mustSelectSameOwner = _mustSelectSameOwner;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetActionStateServerRpc (byte newState) {
        gd.ActionState = (GameData.ActionStates)newState;

    }

    public void Scry(bool player, byte numScry) {
        Debug.Log("NumScry: " + numScry);
        gd.ActionState = GameData.ActionStates.scrying;
        SetActionStateClientRpc(player, (byte)GameData.ActionStates.scrying);
        gd.hasPriority.Value = 2;
        Card[] scriedCards = new Card[numScry];
        for (int i = 0; i < numScry; i++) {
            scriedCards[i] = gd.GS[DECK][0, i];
            scriedCards[i].zone = MOVABLE;
            scriedCards[i].indexInArray = (byte)i;
            gd.GS[DECK][0, i] = gd.nullCard;
            gd.numCards[DECK][0]--;
        }

        for (int i = 0; i < gd.numCards[DECK][0]; i++) {
            gd.GS[DECK][0, i] = gd.GS[DECK][0, i + numScry];
            gd.GS[DECK][0, i].indexInArray = (byte)i;
        }
        for (int i = gd.numCards[DECK][0]; i < GLC.MAX_DECK_SIZE; i++) {
            gd.GS[DECK][0, i] = gd.nullCard;
        }

        SyncDeckServerSide(); //Cannot be run client side


        for (int i = 0; i < numScry; i++) {
            gd.GS[MOVABLE][0, i] = scriedCards[i];
        }
        gd.numCards[MOVABLE][0] = numScry;
        SyncMovableServerSide(player);

        //ScryClientRpc(player, numScry);
    }

    [ClientRpc]
    public void ScryClientRpc(bool player, byte numScry) {
        if (player == IsHost) {
            
        }
    }

    


    void UntapPhase() {
        gd.hasPlayedLand.Value = false;
        gd.hasPriority.Value = 2; //Players don't get priority during untap step
        gd.phase.Value++; // Pass to next phase

        for (byte i = 0; i < GLC.maxLands; i++) {
            gd.GS[LANDS][gd._Ap, i].untapped = true;
        }
        for (byte i = 0; i < GLC.maxCreatures; i++) {
            gd.GS[CREATURES][gd._Ap, i].untapped = true;
            gd.GS[CREATURES][gd._Ap, i].isSummoningSick = false;
        }


        SyncLandsServerSide(gd._Ap);
        SyncCreaturesServerSide(gd._Ap);
    }

    void UpkeepPhase () {

        if (gd.startOfPhase) {
            gd.startOfPhase = false;

            //Add upkeep draw triggers to the stack
            for (int i = 0; i < 9; i++) {
                if (gd.upkeepDraws[gd._Ap, i] == 1) {
                    // Add trigger to stack
                    gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(4, gd.numCards[STACK][0], STACK, gd._Ap, this, gd);
                    gd.numCards[STACK][0]++;
                }else if (gd.upkeepDraws[gd._Ap, i] == 2) {
                    // Add trigger to stack
                    gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(5, gd.numCards[STACK][0], STACK, gd._Ap, this, gd);
                    gd.numCards[STACK][0]++;
                }
            }
            for (int i = 0; i < 9; i++) {
                if (gd.upkeepDraws[(gd._Ap+1)%2, i] == 1) {
                    // Add trigger to stack
                    gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(4, gd.numCards[STACK][0], STACK, (byte)((gd._Ap+1)%2), this, gd);
                    gd.numCards[STACK][0]++;
                }else if (gd.upkeepDraws[(gd._Ap+1)%2, i] == 2) {
                    // Add trigger to stack
                    gd.GS[STACK][0, gd.numCards[STACK][0]] = new Card(5, gd.numCards[STACK][0], STACK, (byte)((gd._Ap+1)%2), this, gd);
                    gd.numCards[STACK][0]++;
                }
            }
            SyncStackServerSide();

            for (int i = 0; i < 9; i++) {
                gd.upkeepDraws[0, i] = 0;
                gd.upkeepDraws[1, i] = 0;
            }

            gd.hasPriority.Value = gd._Ap;
        }

    }

    void MainPhase () {
        if (gd.startOfPhase) {
            gd.startOfPhase = false;
            gd.hasPriority.Value = gd._Ap;
            
        }

    }

    [ServerRpc(RequireOwnership = false)]
    public void ResolveOneFromStackServerRpc(byte targetZone = 255, byte targetOwner = 255, byte targetIndex = 255) {
        resolveOneFromStack(targetZone, targetOwner, targetIndex);
    }

    void resolveOneFromStack (byte targetZone = 255, byte targetOwner = 255, byte targetIndex = 255) {
        Card cardToPlay = gd.GS[STACK][0, gd.numCards[STACK][0]-1];
        byte player = boolToByte(cardToPlay.owner);

        if (cardToPlay.id == 1) {
            //Dandan
            //gd.GS[CREATURES][player, gd.numCards[CREATURES][player]] = cardToPlay;
            //gd.GS[CREATURES][player, gd.numCards[CREATURES][player]].indexInArray = gd.numCards[CREATURES][player];
            //gd.GS[CREATURES][player, gd.numCards[CREATURES][player]].zone = 3;
            //gd.numCards[CREATURES][player]++;
            AddCardTo(CREATURES, player, id:1);
            Debug.Log("gd.numCards[STACK][0]: " + gd.numCards[STACK][0]);
            RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
            //gd.GS[STACK][0, 0] = gd.nullCard;
            //gd.numCards[STACK][0]--;
            
            SyncCreaturesServerSide(player);
            SyncStackServerSide();
            //SyncStackServerSide();

            //Return priority to the active player after a spell/effect resolves
            gd.hasPriority.Value = gd._Ap;
            
        }else if (cardToPlay.id == 2) {
            //Memory Lapse
            //Counter Targeted Spell
            //Assume spell is on the stack
            //Add countered card to top of deck
            Card targetCard = gd.GS[STACK][0, cardToPlay.target.targetIndex];
            //PutInDeck(targetCard.id, 0);
            AddCardTo(DECK, 2, 0, id:targetCard.id);
            gd.GS[DECK][0, 0].knownTo[0] = true;
            gd.GS[DECK][0, 0].knownTo[1] = true;

            SyncDeckServerSide();

            //Remove spell from stack
            RemoveCardFrom(STACK, 2, cardToPlay.target.targetIndex);
            /*
            gd.GS[STACK][0, cardToPlay.target.targetIndex] = gd.nullCard;
            for (int i = cardToPlay.target.targetIndex; i < gd.numCards[STACK][0]; i++) {
                gd.GS[STACK][0, i] = gd.GS[STACK][0, i+1];
                gd.GS[STACK][0, i].indexInArray = (byte)i;
            }
            gd.numCards[STACK][0]--;
            */

            //Remove Memory Lapse from the stack
            AddCardTo(GRAVEYARD, 2, id:2);
            RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
            /*
            gd.GS[GRAVEYARD][0,  gd.numCards[GRAVEYARD][0]] = gd.GS[STACK][0, cardToPlay.indexInArray];
            gd.GS[GRAVEYARD][0,  gd.numCards[GRAVEYARD][0]].indexInArray = gd.numCards[GRAVEYARD][0];
            gd.GS[GRAVEYARD][0,  gd.numCards[GRAVEYARD][0]].zone = GRAVEYARD;
            gd.numCards[GRAVEYARD][0]++;
            */

            //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
            //gd.numCards[STACK][0]--;

            SyncGraveyardServerSide();
            SyncStackServerSide();

            //Return priority to the active player after a spell/effect resolves
            gd.hasPriority.Value = gd._Ap;
            
        }else if (cardToPlay.id == 3) {
            // Lat-Nam's Legacy
            //If you don't have a card in hand just move to graveyard
            if (gd.numCards[HAND][boolToByte(cardToPlay.owner)] > 0) {
                if (targetZone == 255) {
                    //Need to choose a target card to discard
                    Debug.Log("Choose Targets for Latnam's Legacy");
                    Debug.Log("Index In Array: " + cardToPlay.indexInArray);
                    SetActionStateClientRpc(cardToPlay.owner, (byte)GameData.ActionStates.choosingTargets, new bool[7] {false, true, false, false, false, false, false}, STACK, boolToByte(cardToPlay.owner), cardToPlay.indexInArray, true);
                    gd.hasPriority.Value = 2;
                }else {
                    //Player has already choosen the card to discard
                    Debug.Log("Finish resolving LatNam's Legacy");
                    Debug.Log("Card To Shuffle Id: " + gd.GS[targetZone][targetOwner, targetIndex].id);

                    //Add the upkeep trigger
                    int upkeepCounter = 0;
                    while (gd.upkeepDraws[boolToByte(gd.GS[STACK][0, cardToPlay.indexInArray].owner), upkeepCounter] != 0) {
                        upkeepCounter++;
                    }
                    gd.upkeepDraws[boolToByte(gd.GS[STACK][0, cardToPlay.indexInArray].owner), upkeepCounter] = 2;


                    //Remove Latnam's Legacy from the stack
                    AddCardTo(GRAVEYARD, 2, id:3);
                    RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
                    //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = gd.GS[STACK][0, cardToPlay.indexInArray];
                    //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]].indexInArray = gd.numCards[GRAVEYARD][0];
                    //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]].zone = GRAVEYARD;
                    //gd.numCards[GRAVEYARD][0]++;

                    //Shuffle targeted card into deck
                    AddCardTo(DECK, 2, id:gd.GS[targetZone][targetOwner, targetIndex].id);
                    RemoveCardFrom(HAND, targetOwner, targetIndex);
                    /*
                    gd.GS[DECK][0, gd.numCards[DECK][0]] = new Card(gd.GS[targetZone][targetOwner, targetIndex].id, gd.numCards[DECK][0], DECK, 2, this, gd);
                    gd.GS[targetZone][targetOwner, targetIndex] = gd.nullCard;

                    for (int i = targetIndex; i < GLC.MAX_MAX_HAND_SIZE-1; i++) {
                        gd.GS[HAND][targetOwner, i] = gd.GS[HAND][targetOwner, i+1];
                        gd.GS[HAND][targetOwner, i].indexInArray = (byte)i;
                    }
                    gd.GS[HAND][targetOwner, GLC.MAX_MAX_HAND_SIZE-1] = gd.nullCard;
                    gd.numCards[HAND][targetOwner]--;
                    */

                    Shuffle();

                    //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
                    //gd.numCards[STACK][0]--;

                    SyncHandServerSide(targetOwner);
                    SyncGraveyardServerSide();
                    SyncDeckServerSide();
                    SyncStackServerSide();

                    //Return priority to the active player after a spell/effect resolves
                    gd.hasPriority.Value = gd._Ap;
                }
            }else {
                //Didn't have any cards in hand
                AddCardTo(GRAVEYARD, 2, id:(byte)(gd.numCards[STACK][0]-1));
                //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = cardToPlay;
                //gd.numCards[GRAVEYARD][0]++;
                SyncGraveyardServerSide();

                //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
                //gd.numCards[STACK][0]--;
                RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
                SyncStackServerSide();

                gd.hasPriority.Value = gd._Ap;
            }
            
        }else if (cardToPlay.id == 4) {
            DrawCardServerSide(boolToByte(cardToPlay.owner));
            RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
            //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
            //gd.numCards[STACK][0]--;
            //Return priority to the active player after a spell/effect resolves
            gd.hasPriority.Value = gd._Ap;
            
        }else if (cardToPlay.id == 5) {
            DrawCardServerSide(boolToByte(cardToPlay.owner), 2);
            RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
            //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
            //d.numCards[STACK][0]--;
            //Return priority to the active player after a spell/effect resolves
            gd.hasPriority.Value = gd._Ap;
        }else if (cardToPlay.id == 6) {
            //Chemister's Insight

            //If cast from hand
            //Remove from stack, add to graveyard, draw two cards
            if (cardToPlay.exileOnResolution) {
                //gd.GS[EXILE][0, gd.numCards[EXILE][0]] = new Card(cardToPlay.id, gd.numCards[EXILE][0], EXILE, 2, this, gd);
                //gd.numCards[EXILE][0]++;
                AddCardTo(EXILE, 2, id:(byte)(gd.numCards[STACK][0]-1));
                SyncExileServerSide();
            }else {
                //gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = new Card(cardToPlay.id, gd.numCards[GRAVEYARD][0], GRAVEYARD, 2, this, gd);
                //gd.numCards[GRAVEYARD][0]++;
                AddCardTo(GRAVEYARD, 2, id:(byte)(gd.numCards[STACK][0]-1));
                SyncGraveyardServerSide();
            }

            //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
            //gd.numCards[STACK][0]--;
            RemoveCardFrom(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
            SyncStackServerSide();

            DrawCardServerSide(boolToByte(cardToPlay.owner), 2);

            //If cast from graveyard
            //Remove from stack, add to exile, draw two cards
            gd.hasPriority.Value = gd._Ap;
        }else if (cardToPlay.id == 7) {
            //Tamiyo's Epiphany
            

            //Scry 4
            Scry(cardToPlay.owner, 4);
            //DrawCardServerSide(boolToByte(cardToPlay.owner), 2);
            //gd.GS[STACK][0, cardToPlay.indexInArray] = gd.nullCard;
            //gd.numCards[STACK][0]--;
            SyncStackServerSide();
            
            
    
            
        }else{
            //Just delete the card if we don't know what it is
            gd.GS[STACK][0, 0] = gd.nullCard;
            gd.numCards[STACK][0]--;
            Debug.Log("Unknown card to resolve.");
            //Return priority to the active player after a spell/effect resolves
            gd.hasPriority.Value = gd._Ap;
        }



        /*
        for (int i = 0; i < GLC.maxStackSize-1; i++) {
            gd.GS[STACK][0, i] = gd.GS[STACK][0, i+1];
            gd.GS[STACK][0, i].indexInArray = (byte)i;
        }*/
        //gd.GS[STACK][0, GLC.maxStackSize-1] = gd.nullCard;

        SyncStackServerSide();
    }



    void Attacks () {
        if (gd.startOfPhase) {
            gd.startOfPhase = false;
            Debug.Log("Attacks");
            if (gd.Ap.Value) {
                Debug.Log("Player 1's Turn");
                Debug.Log("P1 Passing for Turn: " + gd.passingForRestOfTurnP1);
                if (gd.passingForRestOfTurnP1) {
                    Debug.Log("P1 was passing for turn");
                    gd.hasPriority.Value = 0;
                    return;
                }
            }else{
                Debug.Log("Player 0's Turn");
                Debug.Log("P0 Passing for Turn: " + gd.passingForRestOfTurnP0);
                if (gd.passingForRestOfTurnP0) {
                    Debug.Log("P0 was passing for turn");
                    gd.hasPriority.Value = 1;
                    return;
                }
            }

            SetInstructionClientRpc(gd.Ap.Value, "Attack");

            //setNumValues.SetInstructionTxt("Declare Attacks");
            //gd.selectTag = "canAttack";
            gd.hasPriority.Value = 2;
            //Ap needs to delare attacks before they get priority

            //confirmBtn.SetActive(true);
        }
    }

    void Blocks () {

        if (gd.startOfPhase) {
            gd.startOfPhase = false;
            //Go to end of combat step if no attackers were declared
            int numAttackers = 0;
            for (int i = 0; i < gd.numCards[CREATURES][gd._Ap]; i++) {
                if (gd.GS[CREATURES][gd._Ap, i].isAttackOrBlocking) numAttackers++;
            }
            if (numAttackers == 0) {
                gd.phase.Value = 8;
                gd.hasPriority.Value = gd._Ap;
                return;
            }
            SetInstructionClientRpc(!gd.Ap.Value, "Block");
            //setNumValues.SetInstructionTxt("Declare Blocks");
            gd.hasPriority.Value = 2;
            //Non-Ap needs to delare blocks before they get priority
            //confirmBtn.SetActive(true);
        }
    }

    void Damage () {
        if (gd.startOfPhase) {
            gd.startOfPhase = false;
            //setNumValues.SetInstructionTxt("");
            int damageToPlayer = 0;

            for (int i = 0; i < GLC.maxCreatures; i++) {
                if (gd.GS[CREATURES][gd._Ap, i] != gd.nullCard) {
                    if (gd.GS[CREATURES][gd._Ap, i].isAttackOrBlocking) {
                        damageToPlayer += 4;
                    }
                }
            }

            //subtract damage that was blocked
            for (byte i = 0; i < GLC.maxCreatures; i++) {
                if (gd.GS[CREATURES][(gd._Ap+1)%2, i] != gd.nullCard) {
                    if (gd.GS[CREATURES][(gd._Ap+1)%2, i].isAttackOrBlocking) {
                        damageToPlayer -= 4;

                        //Kill an attacking and blocking Dandan
                        //Find an attacking Dandan
                        for (byte j = 0; i < gd.numCards[CREATURES][gd._Ap]; j++) {
                            if (gd.GS[CREATURES][gd._Ap, j].isAttackOrBlocking) {
                                //Found an attacking Dandan

                                gd.GS[CREATURES][gd._Ap, j] = new Card(255, j, CREATURES, gd._Ap, this, gd);
                                for (byte k = j; k < gd.numCards[CREATURES][gd._Ap] - 1; k++) {
                                    gd.GS[CREATURES][gd._Ap, k] = gd.GS[CREATURES][gd._Ap, k + 1];
                                    gd.GS[CREATURES][gd._Ap, k].indexInArray = k;
                                }
                                gd.GS[CREATURES][gd._Ap, gd.numCards[CREATURES][gd._Ap] - 1] = new Card(255, (byte)(gd.numCards[CREATURES][gd._Ap] - 1), CREATURES, gd._Ap, this, gd);
                                gd.numCards[CREATURES][gd._Ap]--;

                                gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = new Card(1, gd.numCards[GRAVEYARD][0], GRAVEYARD, 2, this, gd);
                                gd.numCards[GRAVEYARD][0]++;
                                break;
                            }
                        }

                        //Kill this blocking Dandan

                        gd.GS[CREATURES][(gd._Ap+1)%2, i] = new Card(255, i, CREATURES, (byte)((gd._Ap+1)%2), this, gd);
                        for (byte k = i; k < gd.numCards[CREATURES][(gd._Ap+1)%2] - 1; k++) {
                            gd.GS[CREATURES][(gd._Ap+1)%2, k] = gd.GS[CREATURES][(gd._Ap+1)%2, k + 1];
                            gd.GS[CREATURES][(gd._Ap+1)%2, k].indexInArray = k;
                        }
                        gd.GS[CREATURES][(gd._Ap+1)%2, gd.numCards[CREATURES][(gd._Ap+1)%2] - 1] = new Card(255, (byte)(gd.numCards[CREATURES][(gd._Ap+1)%2] - 1), CREATURES, (byte)((gd._Ap+1)%2), this, gd);
                        gd.numCards[CREATURES][(gd._Ap+1)%2]--;
                        i--;
                        gd.GS[GRAVEYARD][0, gd.numCards[GRAVEYARD][0]] = new Card(1, gd.numCards[GRAVEYARD][0], GRAVEYARD, 2, this, gd);
                        gd.numCards[GRAVEYARD][0]++;

                    }
                }
            }
            if (damageToPlayer > 0) {
                gd.lifeTotal[(gd._Ap+1)%2] -= damageToPlayer; 
                SyncLifeServerSide((byte)((gd._Ap+1)%2));
            }

            SyncCreaturesServerSide(1);
            SyncCreaturesServerSide(0);
            SyncGraveyardServerSide();
        }

    }

    void RemoveAllFromCombat() {
        for(int i = 0; i < gd.numCards[CREATURES][1]; i++) {
            gd.GS[CREATURES][1, i].isAttackOrBlocking = false;
        }
        for(int i = 0; i < gd.numCards[CREATURES][0]; i++) {
            gd.GS[CREATURES][0, i].isAttackOrBlocking = false;
        }

        SyncCreaturesServerSide(1);
        SyncCreaturesServerSide(0);
    }

    [ServerRpc(RequireOwnership = false)]
    void PassPriorityServerRpc(bool player) {
        if (player) {
            gd.hasPassedP1.Value = true;
            gd.hasPriority.Value = 0;
        }else {
            gd.hasPassedP0.Value = true;
            gd.hasPriority.Value = 1;
        }
    }


    [ServerRpc(RequireOwnership = false)]
    void PassForTurnServerRpc(bool player) {
        if (player) {
            gd.passingForRestOfTurnP1 = true;
        }else {
            gd.passingForRestOfTurnP0 = true;
            Debug.Log("P0 Passing for Turn: " + gd.passingForRestOfTurnP0);
        }
        //gd.passingForRestOfTurn = false;
    }




    // Update is called once per frame
    void Update()
    {


        if (IsClient) {
            if (gd.Ap.Value) {
                gd._Ap = 1;
            }else {
                gd._Ap = 0;
            }
        }

        if (IsClient) {
            //Double check game state
            byte[] Maxes = new byte[8] {GLC.MAX_DECK_SIZE, GLC.MAX_MAX_HAND_SIZE, GLC.maxStackSize, GLC.maxCreatures, GLC.maxLands, GLC.maxGraveyard, GLC.maxExile, GLC.maxMovableCards};
            for (int z = 0; z < 8; z++) {
                for (int p = 0; p < 1; p++) {
                    if ((z == 2 || z == 5 || z == 6) && p == 1) continue;
                    int counter = 0;
                    for (int i = 0; i < Maxes[z]; i++) {
                        //Debug.Log(z + ", " + p + ", " + i);
                        if (gd.GS[z][p, i].id != 255) {
                            counter++;

                            if (gd.GS[z][p, i].zone != z) {
                                Debug.Log(z + "[" + p + ", " + i + "]'s zone is unsynced");
                            }
                            if (gd.GS[z][p, i].indexInArray != i) {
                                Debug.Log(z + "[" + p + ", "  + i + "]'s index in array is unsynced");
                            }
                        }
                    }
                    if (counter != gd.numCards[z][p]) {
                        Debug.Log("The number of cards in " + z + ", " + p + " is unsynced");
                    }
                }
            }
        }
        





    
        //Run the game by managing the player's turns
        if (IsServer) {
            //If the stack ever has cards on it we must resolve them
            if (gd.numCards[STACK][0] != 0) {
                //if both players have passed on priority resolve one card
                //else do nothing and wait for the players to cast spells or pass priority
                if (gd.hasPassedP1.Value && gd.hasPassedP0.Value) {
                    //Resolve top spell of the stack
                    gd.hasPassedP1.Value = false;
                    gd.hasPassedP0.Value = false;
                    resolveOneFromStack();
                }else {
                    
                }
            }else {
                //the stacksize == 0
                //The active player should get priority and then if they pass the nonAp should get priority
                if (gd.hasPriority.Value != 2) {
                    if (gd._Ap == 1) {
                        //P1 is the active Player
                        if (gd.hasPassedP1.Value == false) {
                            gd.hasPriority.Value = 1;
                        }else if (gd.hasPassedP0.Value == false) {
                            gd.hasPriority.Value = 0;
                        }else {
                            //Pass the phase
                            gd.hasPassedP1.Value = false;
                            gd.hasPassedP0.Value = false;
                            gd.hasPriority.Value = 2;
                            
                            gd.phase.Value = (byte)((gd.phase.Value + 1) % 12);
                            gd.startOfPhase = true;
                        }
                    }else {
                        //P0 is the active player
                        if (gd.hasPassedP0.Value == false) {
                            gd.hasPriority.Value = 0;
                        }else if (gd.hasPassedP1.Value == false) {
                            gd.hasPriority.Value = 1;
                        }else {
                            //Pass the phase
                            gd.hasPassedP1.Value = false;
                            gd.hasPassedP0.Value = false;
                            
                            gd.hasPriority.Value = 2;
                            
                            gd.phase.Value = (byte)((gd.phase.Value + 1) % 12);
                            gd.startOfPhase = true; 
                        }
                    }
                }
            }
        }


        //Hacks
        if (IsServer) {
            if (Input.GetKeyDown(KeyCode.C)) { // C to draw a card
                DrawCardServerSide(gd._Ap);
            }
            if (Input.GetKeyDown(KeyCode.P)) { // P to resolve a card from the stack
                resolveOneFromStack();
            }
            if (Input.GetKeyDown(KeyCode.N)) {
                gd.phase.Value = (byte)((gd.phase.Value + 1) % 12);
                
                gd.startOfPhase = true;
            }
        }


        if (IsServer) {
            if (gd.passingForRestOfTurnP1 && gd.hasPriority.Value == 1) {
                PassPriorityServerRpc(true);
            }
            if (gd.passingForRestOfTurnP0 && gd.hasPriority.Value == 0) {
                PassPriorityServerRpc(false);
            }
        }


        if (IsClient) {
            if (gd.ActionState == GameData.ActionStates.normal) { 
                //Normal mode

                //Change priority if key is pressed
                if (gd.hasPriority.Value != 2) {
                    
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
                        if (Input.GetKeyDown(KeyCode.Space)) {
                            if (IsHost) {
                                PassForTurnServerRpc(true);
                            }else {
                                PassForTurnServerRpc(false);
                            }
                        }
                    }
                    /*
                    if (gd._Ap == 1) {
                        if (gd.hasPassedP1.Value == false && IsHost && Input.GetKeyDown(KeyCode.Space)) {
                            //Player 1 passes
                            PassPriorityServerRpc(true);
                        }else if (gd.hasPassedP0.Value == false && gd.hasPassedP1.Value && !IsHost && Input.GetKeyDown(KeyCode.Space)) {
                            //P1 has passed but P0 hasn't
                            //Player 0 passes
                            PassPriorityServerRpc(false);
                        }
                    }else {
                        if (gd.hasPassedP0.Value == false && !IsHost && Input.GetKeyDown(KeyCode.Space)) {
                            //Player 1 passes
                            PassPriorityServerRpc(false);
                        }else if (gd.hasPassedP1.Value == false && gd.hasPassedP0.Value && IsHost && Input.GetKeyDown(KeyCode.Space)) {
                            //P1 has passed but P0 hasn't
                            //Player 0 passes
                            PassPriorityServerRpc(true);
                        }
                    }
                    */
                    if (gd.hasPriority.Value == 1 && gd.hasPassedP1.Value == false && IsHost && Input.GetKeyDown(KeyCode.Space)) {
                        PassPriorityServerRpc(true);
                    }else if (gd.hasPriority.Value == 0 && gd.hasPassedP0.Value == false && !IsHost && Input.GetKeyDown(KeyCode.Space)) {
                        PassPriorityServerRpc(false);
                    }
                }
            }else if(gd.ActionState == GameData.ActionStates.choosingTargets) {
                //Choosing Targets
            }
        }
        




        if (IsServer) {
            //Priority has been assigned for the update
            //Run the phase
            if (gd.phase.Value == 0) {
                //Untap Resets things that need to reset every turn such as untapping the cards, removing summoning sickness, allowing the player to make another land drop
                UntapPhase();
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 1) {
                //Upkeep
                UpkeepPhase();
            }else if (gd.phase.Value == 2) {
                //Draw
                DrawCardServerSide(gd._Ap);
                gd.phase.Value++;
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 3) {
                //Main phase 1
                MainPhase();
            }else if (gd.phase.Value == 4) {
                //Begin Combat
                gd.phase.Value++;
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 5) {
                //Attacks
                Attacks();
            }
            else if (gd.phase.Value == 6) {
                //Blocks
                Blocks();
            }else if (gd.phase.Value == 7) {
                //Damage
                Damage();
                gd.phase.Value++;
                gd.startOfPhase = true;
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 8) {
                //End of Combat
                gd.phase.Value++;
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 9) {
                //Main phase 2
                RemoveAllFromCombat();
                MainPhase();
            }else if (gd.phase.Value == 10) {
                //Endstep
                
                gd.phase.Value++;
                gd.hasPassedP0.Value = false;
                gd.hasPassedP1.Value = false;
            }else if (gd.phase.Value == 11) {
                //Cleanup
                gd.phase.Value = 0;
                gd.Ap.Value = !gd.Ap.Value;
                
                gd.passingForRestOfTurnP1 = false;
                gd.passingForRestOfTurnP0 = false;
                gd.hasPassedP1.Value = false;
                gd.hasPassedP0.Value = false;
            }
        }


       


    }
   

}

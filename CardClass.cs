using Unity.Collections;
using UnityEngine;

//How Card Data is stored

//Declares the card class
public class Card {

    const int DECK = 0;                   
    const int HAND = 1;
    const int STACK = 2;
    const int CREATURES = 3;
    const int LANDS = 4;
    const int GRAVEYARD = 5;
    const int EXILE = 6;

    public enum cardTypes {land, creature, instant, sorcery, trigger};

    private PlayerScript PS;
    private GameData gd;
    public byte id;
    public byte indexInArray;
    public byte zone;
    //public string name;
    public bool untapped = true;
    public bool isAttackOrBlocking = false;

    public bool isSummoningSick = false;

    public bool owner;

    public Target target = new Target(255, 255, 255, 255, 255, 255);

    public byte numTargetsToCast;
    public byte colorlessMana;
    public byte blueMana;
    public cardTypes cardType;

    public bool[] knownTo = new bool[2] {false, false};

    public bool exileOnResolution = false;


    //The prefab representing the card

    //Delete physical????
    //public GameObject physical;
    // physical.transform.position => (x, y, z)

    public Card(byte _id, byte _indexInArray, byte _zone, byte _owner, PlayerScript _PS, GameData _gd) {
        id = _id;
        indexInArray = _indexInArray;
        zone = _zone;
        owner = byteToBool(_owner);
        PS = _PS;
        gd = _gd;

        setUpCardAttributes(id);        


    }


    void setUpCardAttributes(byte id) {
        switch (id) {
            case 0:
                //Island
                cardType = cardTypes.land;
                colorlessMana = 0;
                blueMana = 0;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 1:
                //Dandan
                cardType = cardTypes.creature;
                colorlessMana = 0;
                blueMana = 2;
                numTargetsToCast = 0;
                isSummoningSick = true;
                break;
            case 2:
                //Memory Lapse
                cardType = cardTypes.instant;
                colorlessMana = 1;
                blueMana = 1;
                numTargetsToCast = 1;
                isSummoningSick = false;
                break;
            case 3:
                //LatNam's Legacy
                cardType = cardTypes.instant;
                colorlessMana = 1;
                blueMana = 1;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 4:
                //Draw a Card Upkeep Trigger
                cardType = cardTypes.trigger;
                colorlessMana = 0;
                blueMana = 0;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 5:
                //Draw two Cards Upkeep Trigger
                cardType = cardTypes.trigger;
                colorlessMana = 0;
                blueMana = 0;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 6:
                //chemister's Insight
                cardType = cardTypes.instant;
                colorlessMana = 3;
                blueMana = 1;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 7:
                //Tamiyo's Epiphany
                cardType = cardTypes.sorcery;
                colorlessMana = 3;
                blueMana = 1;
                numTargetsToCast = 0;
                isSummoningSick = false;
                break;
            case 8:
                //Fetid Pools
                cardType = cardTypes.land;
                colorlessMana = 0;
                blueMana = 0;
                numTargetsToCast = 0;
                isSummoningSick = false;
                untapped = false;
                break;
        }
    }

    public bool canCast() {
        switch (id) {
            case 0:                 //Island
                // Check if land drop has been made, has priority, emtpy stack, is main phase, is owner's turn, is owner, in hand
                if (!gd.hasPlayedLand.Value && gd.numCards[STACK][0] == 0 && gd.hasPriority.Value == boolToByte(owner) && boolToByte(owner) == gd._Ap && (gd.phase.Value == 3 || gd.phase.Value == 9) && owner == PS.GetIsHost() && zone == HAND) return true;
                else return false;
            case 1:                 //Dandan
                if (PS.CanPayMana(boolToByte(owner), colorlessMana, blueMana) && gd.numCards[STACK][0] == 0 && boolToByte(owner) == gd._Ap && gd.hasPriority.Value == boolToByte(owner) && (gd.phase.Value == 3 || gd.phase.Value == 9) && owner == PS.GetIsHost() && zone == HAND) return true;
                else return false;
            case 2:                 //Memory Lapse
                if (PS.CanPayMana(boolToByte(owner), colorlessMana, blueMana) && boolToByte(owner) == gd.hasPriority.Value && gd.numCards[STACK][0] != 0 && owner == PS.GetIsHost() && zone == HAND) return true;
                else return false;
            case 3:                 //LatNam's Legacy
                if (PS.CanPayMana(boolToByte(owner), colorlessMana, blueMana) && boolToByte(owner) == gd.hasPriority.Value && owner == PS.GetIsHost() && zone == HAND) return true;
                else return false;
            case 4:                 //Draw a Card Trigger
                Debug.Log("Tried to cast \"Draw a Card Trigger\"");
                break;
            case 5:                 //Draw two Cards Trigger
                Debug.Log("Tried to cast \"Draw two Cards Trigger\"");
                break;
            case 6:                 //Chemister's Insight
                //Can cast in hand or graveyard
                if (PS.CanPayMana(boolToByte(owner), colorlessMana, blueMana) && boolToByte(owner) == gd.hasPriority.Value && owner == PS.GetIsHost() && (zone == HAND || (zone == GRAVEYARD && gd.numCards[HAND][boolToByte(owner)] > 0)) ) return true;
                else return false;
            case 7:                 //Tamiyo's Epiphany
                if (boolToByte(owner) == gd.hasPriority.Value && PS.CanPayMana(boolToByte(owner), 3, 1) && gd.numCards[STACK][0] == 0 && (gd.phase.Value == 3 || gd.phase.Value == 9) && owner == gd.Ap.Value) return true;
                else return false;
            case 8:                 //Fetid Pools
                // Check if land drop has been made, has priority, emtpy stack, is main phase, is owner's turn, is owner, in hand
                if (!gd.hasPlayedLand.Value && gd.numCards[STACK][0] == 0 && gd.hasPriority.Value == boolToByte(owner) && boolToByte(owner) == gd._Ap && (gd.phase.Value == 3 || gd.phase.Value == 9) && owner == PS.GetIsHost() && zone == HAND) return true;
                else return false;
        }
        return false;
    }

    byte boolToByte(bool _bool) {
        if (_bool) return 1;
        else return 0;
    }

    bool byteToBool(byte _byte) {
        if (_byte == 0) return false;
        else return true;
    }

    public void OnClick () {

        if (PS.GetIsHost() != owner && !(zone == 2 || zone == 5 || zone == 6)) return;
        Debug.Log("CardClass OnClick");
        
        if (zone == 1) {
            //Card must be in hand to play

            //check if card can be played
            if (!canCast()) return;
            switch (id) {
                case 0:                 //Island
                    AttemptToPlay();
                    break;
                case 1:                 //Dandan
                    AttemptToPlay();
                    break;
                case 2:                 //Memory Lapse
                    //Make sure there is a target
                    if (gd.numCards[STACK][0] == 0) break;

                
                    gd.ActionState = GameData.ActionStates.choosingTargets;
                    gd.selectZones[STACK] = true;
                    gd.globalTarget.sourceOwner = boolToByte(owner);
                    gd.globalTarget.sourceIndex = indexInArray;
                    gd.globalTarget.sourceZone = zone;
                    gd.mustSelectSameOwner = false;
                    break;

                case 3:                 //LatNam's Legacy
                    AttemptToPlay();
                    break;
                case 4:                 //Draw a Card Trigger
                    Debug.Log("Tried to play \"Draw a Card Trigger\"");
                    break;
                case 5:                 //Draw two Cards Trigger
                    Debug.Log("Tried to play \"Draw two Cards Trigger\"");
                    break;
                case 6:                 //Chemister's Insight
                    AttemptToPlay();
                    break;
                case 7:                 //Tamiyo's Epiphany
                    AttemptToPlay();
                    break;
                case 8:                 //Fetid Pools
                    AttemptToPlay();
                    break;
            }
        }else if (zone == 3) {
            //Card is in play
            InPlayClick();
        }else if (zone == GRAVEYARD) {
            Debug.Log("Clicked on cark in graveyard");
            switch(id) {
                case 6:
                    if (gd.numCards[HAND][boolToByte(owner)] == 0) break;

                    Debug.Log ("Choose card to discard for Chemister's Insight");

                
                    gd.ActionState = GameData.ActionStates.choosingTargets;
                    gd.selectZones[HAND] = true;
                    gd.globalTarget.sourceOwner = boolToByte(owner);
                    gd.globalTarget.sourceIndex = indexInArray;
                    gd.globalTarget.sourceZone = zone;
                    gd.mustSelectSameOwner = true;
                    break;
            }
        }else {
            Debug.Log("Clicked on card in zone: " + zone);
        }
    }

    public void AfterTargetsChoosen() {
        Debug.Log("AfterTargetsChoosen; id: " + id);
        switch (id) {
            case 0:
                break;
            case 1:
                break;
            case 2:
                AttemptToPlay();
                break;
            case 3:                 //Play Lat-Nam's Legacy
                Debug.Log("ReTry resolving; targetZone: " + gd.globalTarget.targetZone + ", targetOwner: " + gd.globalTarget.targetOwner + ", targetIndex: " + gd.globalTarget.targetIndex);
                PS.ResolveOneFromStackServerRpc(gd.globalTarget.targetZone, gd.globalTarget.targetOwner, gd.globalTarget.targetIndex);
                break;
            case 4:                 //Draw a Card Trigger
                Debug.Log("Tried to play \"Draw a Card Trigger\"");
                break;
            case 5:                 //Draw two Cards Trigger
                Debug.Log("Tried to play \"Draw two Cards Trigger\"");
                break;
            case 6:                 //Chemister's Insight Jump-Start
                AttemptToPlay();
                break;
            case 7:                 //Tamiyo's Epiphany
                PS.AddCardToServerRpc(GRAVEYARD, 2, id:id);
                PS.RemoveCardFromServerRpc(STACK, 2, (byte)(gd.numCards[STACK][0]-1));
                PS.SyncStackServerRpc();
                PS.DrawCardServerRpc(boolToByte(owner), 2);
                PS.SetActionStateServerRpc((byte)GameData.ActionStates.normal);
                gd.hasPriority.Value = gd._Ap;
                break;
            case 8:
                break;
            case 9:
                break;
            
        }
    }

    public void AttemptToPlay () {
        if (id == 2) {
            
            PS.AttemptToPlay(zone, boolToByte(owner), indexInArray, gd.globalTarget.targetZone,  gd.globalTarget.targetOwner,  gd.globalTarget.targetIndex);
        }else if (id == 6) {
            PS.AttemptToPlay(zone, boolToByte(owner), indexInArray, gd.globalTarget.targetZone,  gd.globalTarget.targetOwner,  gd.globalTarget.targetIndex);
        
        }
        else {
            Debug.Log("Playing Card: " + id);
            PS.AttemptToPlay(zone, boolToByte(owner), indexInArray);
        }
    }

    void InPlayClick() {
        
        if (id == 1) {
            
            if (gd.phase.Value == 5 && owner == gd.Ap.Value && owner == PS.GetIsHost() && gd.hasPriority.Value == 2) {

                

                if (owner) {
                    //Card is Dandan and is attack phase and is owner's turn
                    if (gd.GS[CREATURES][1, indexInArray].isAttackOrBlocking) {
                        PS.SwapAttackingStatusServerRpc(owner, indexInArray);
                        
                    }else {
                        if (!gd.GS[CREATURES][1, indexInArray].isSummoningSick) {
                            PS.SwapAttackingStatusServerRpc(owner, indexInArray);
                        }
                    }
                }else {
                    
                    //P0
                    //Card is Dandan and is attack phase and is owner's turn
                    if (gd.GS[CREATURES][0, indexInArray].isAttackOrBlocking) {
                        PS.SwapAttackingStatusServerRpc(owner, indexInArray);
                    }else {
                        //Add to combat
                    
                        if (!gd.GS[CREATURES][0, indexInArray].isSummoningSick) {
                            
                            PS.SwapAttackingStatusServerRpc(owner, indexInArray);
                        }
                    }
                }
            }else if (gd.phase.Value == 6 && owner != gd.Ap.Value && owner == PS.GetIsHost() && gd.hasPriority.Value == 2) {
                //Blocking
                if (owner) {
                    //P1
                    if (gd.GS[CREATURES][1, indexInArray].untapped) {
                        if (gd.GS[CREATURES][1, indexInArray].isAttackOrBlocking) {
                            //Is currently blocking => remove from combat
                            PS.SwapBlockingStatusServerRpc(owner, indexInArray);
                        }else {
                            PS.SwapBlockingStatusServerRpc(owner, indexInArray);
                        }
                    }
                }else {
                    // P0
                    if (gd.GS[CREATURES][0, indexInArray].untapped) {
                        if (gd.GS[CREATURES][0, indexInArray].isAttackOrBlocking) {
                            //Is currently blocking => remove from combat
                            PS.SwapBlockingStatusServerRpc(owner, indexInArray);
                        }else {
                            PS.SwapBlockingStatusServerRpc(owner, indexInArray);
                        }
                    }
                }
            }
        }
    }
}






public struct Target {
    public byte sourceZone;
    public byte sourceIndex;
    public byte sourceOwner;
    public byte targetZone;
    public byte targetIndex;
    public byte targetOwner;

    public Target(byte _sz, byte _si, byte _so, byte _tz, byte _ti, byte _to) {
        sourceZone = _sz;
        sourceIndex = _si;
        sourceOwner = _so;
        targetZone = _tz;
        targetIndex = _ti;
        targetOwner = _to;
    }
}


public class InstructionSettings {
    public bool player;
    public FixedString32Bytes textId; // Up to at least 10 chars?

    public InstructionSettings(bool _player, FixedString32Bytes _textId) {
        player = _player;
        textId = _textId;
    }
}


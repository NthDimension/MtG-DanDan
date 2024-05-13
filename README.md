# MtG-DanDan
Unity Code for Magic the Gathering format Dandan

**In Progress**

Contains code for the core logic of the game.
UI is handled by other scripts

PlayerScript:
Handles changing the state of the game: changing phase, moving cards between zones, syncing information between host and client

GameData:
Stores the state of the game: what cards are where, whose turn it is, life totals of each player, etc.

CardClass:
Each card in MtG is unique, each having their own cost, time they can be played, and effect on the game.
CardClass is used to store the unique information and rules associated with each card.

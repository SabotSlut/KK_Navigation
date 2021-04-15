KK_Navigation
=============
Modifies the navigation code to allow new gates (warps between maps) that are (mostly) properly integrated into the navigation system.

Bugs
----
* Calling an NPC on a new map may cause them to spawn near the middle of the map when they arrive, rather than at the appropriate gate.
* Gates that teleport you to the same map that they're on do not work.

To Implement
------------
* Additional paths aren't yet added to pre-existing routes. Therefore, NPC navigation won't take any shortcuts you add unless they're starting from or going to a newly-added map.
* Add special logic for gates that target the same map they're on.
* Allow a gate to bring up a menu of potential locations to visit (similar to the teleport menu), in order to make integrating new maps easier for modders.
* Some sort of coherent API? Would probably need use cases.

Implementation Notes
--------------------
* This doesn't use the normal gate system. It uses a custom Gate format to try and ensure maximum interoperability. Gates target other gates by name rather than by ID, and gate IDs are automatically assigned for custom gates.
* This uses a moderately-modified version of [Viacheslav Avsenev's A* Implementation](https://github.com/snmslavk/AStar). For technical reasons, it is integrated directly into the assembly.

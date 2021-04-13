KK_Navigation
=============
Modifies the navigation code to allow new gates (warps between maps) that are (or at least will be) properly integrated into the navigation system.

Notes
-----
* May or may not work with loading into new maps. In my testing, it doesn't work, but that could be due to my test map rather than this plugin.
* Will not work with gates that teleport you to the same map.

To Implement
------------
* Proper distance calculation isn't implemented yet.
* Additional routes aren't yet added to the existing NPC navigation code.

Using
-----
* [Viacheslav Avsenev's A* Implementation](https://github.com/snmslavk/AStar)

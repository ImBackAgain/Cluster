# A little lost?

There’s a lot going here between the Blender file and the custom Editor scripting that you’ve done in Unity.

## Overview

You create files in Blender and export them as FBX files with [certain settings](#export-settings).  In Unity, your custom script mangles the animation clips to create copies that have a few extra bits and don’t have a few unnecessary bits.  These then have to be dragged into the Animator Controller, but we may be able to automate that with a bit more scripting.

## In Blender

### Animating basics

**Cyclic animations need to have ‘Cyc’ on the end**.  Okay, it’s not *vital*; you can make them cyclic in Unity manually after [wrangling them](#animation-adapter).  But for consistency, add the ‘Cyc’.

You have a number of objects using the same ‘Points’ armature, whose names all end in ‘Points’.  This is so the bones can have different Bone Constraints on them.  I expect to animate using a large number of curves, and I don’t want a skyscraper of different Follow Curve constraints on every bone all the time.

There’s also the one articulated armature called ‘Articulations’ (object ‘Artic’), and the Points Artic object (and probably all the others too) have Copy Location constraints to sit on the vertices of this skeleton.  For relevant animations, you may animate Artic while keeping the constraints’ Influnence at 1 to make the Points move articulatedly.

### Organising Actions

First off, I should tell you that I’m using action slots when I have an action that affects different objects.  It’s not a big deal but you ought to be consistent.  E.g. the Idle action has a slot for the Artic object and another for the Points Art object.  The latter only has key frames to set the Influence of the Copy Location constraints.

Anyway, exporting these actions is weird.  The NLA doesn’t seem to help much; I tried using it along with the NLA strips option in the [export settings](#export-settings) to no avail.

I’ve also tried using the ‘Bake Action’ command, and that did work okay before... but I can’t get it to work now.  I think some NLA nonsense was afoot, but I have no idea.

The other option is to export all such actions separately: once you link both objects to the same action, exporting just the Points object does make *the one linked action* work correctly.  It may be a bit too messy, though.

Although... you could just opt to delete all the FBXes afterward...  Okay, we’ll go with this one.  The FBXes will be transient, since we never needed a model for the player anyway.

### Animating permutations

Probably the biggest feature of this project are the ‘permutations’: moments where bones swap places with each other, where the corresponding sphere in game is supposed to leave the old bone and switch to the new bone that’s in the same-ish position instead.

The animations that’ll go out of Blender encode the permutations using the position of the visually unused bone ‘Perm’ (part of the ‘Points’ armature).  On a frame where a permutation should take place, key the position of the Perm bone with specific values that represent the permutation.  You’ve written some Python code that does this from a [cycle notation](https://en.wikipedia.org/wiki/Permutation#Cycle_notation) of the desired permutation.  Just make sure the timeline is at the right frame, go into the Scripting workspace, open the Permutant text file, and run it.  You should probably key the bone back to 0, 0, 0 on the following frame.

Hm?  The exact translation from a permutation into coordinates?  Fine.
* To start, we use the order ABXYTH, mapped on to \[0...5], so 0 is the left foot and so on.
* Next, take [one-line notation](https://en.wikipedia.org/wiki/Permutation#One-line_notation), which is just a sequence of numbers listing where each element will end up.
* Then change each number into a change rather than an absolute number: how many steps ahead the element will move in the list (mod 6, so within \[0...5] again).
* Group them into pairs to get 3 two-digit numbers (base ten, yes).  The first is the x, the second is the y, and the third is the z.  Note that they’re y and z in Blender; in Unity, z is second and y is third.

### Export settings

Ermin has a spreadsheet where I documented my experiments to find the right settings.  Should be in your main Spreadsheets folder.

BTW, you can Shift+Tab to open up Quick Favourites, where I’ve put the FBX export command.

Anyway, the settings are:
* Include
    * Limit to: Selected Objects
* Transform
    * Apply Scalings: FBX Units Scale
	* Forward: Y Forward
	* Up: Z up
	* Do not Apply Units
	* Do not Use Space Transform
* Animations
    * Tick ‘All Actions’ and probably untick ‘NLA Strips’ if you’ve put anything there.  Action names will be prefixed with the object name, e.g. ‘Points Art|Idle Cyc’, so keep that in mind.

## Unity

The files should be put into ‘Assets/FBX Imports’.  Before anything else, get the file up in the Inspector, check ‘Bake axis conversion’, and hit Apply.  Then they’re ready to get processed by the

### Animation Adapter

This fights the animation clips contained in our FBX files.  It creates copies of the animation clips which, hopefully, have all the properties we need.  The main code for this is in ‘Assets/Editor Scripting’ currently.

Oh, right.  It’s a Window.  From Unity’s menu bar, go to Windows → UI Toolkit → AnimAdapter.  You should see a little thingy with three text fields and a few buttons.  Of the text fields, as of this version, you’ll probably only ever use the first.  Of the buttons, the last two are dummy buttons which I made to test random functions.

Here’s what the text inputs are for.

* The first is the file name, without the .fbx extension.
* The second is an ‘object name’; with our chosen export settings, Blender prefixes the animation names with the object name, e.g. ‘Points Art|Idle Cyc’.  In a previous version, I needed to separate this so that the new file would remove this part (the pipe caused issues in the filename); now, the clip is automatically processed to find the post-pipe part, so it’s not an issue.
* The third is the name of the clip to fight.  It will be copied into a standalone file with the same as the clip (plus ‘.anim’).  You can now put a full object-and-pipe-prefixed name here and it won’t hurt anyone.

Any of these can have multiple values separated by a comma and a space.  No other trimming or cleanup of the string happens.

The new clips aren’t exact copies, of course.  It removes everything except the main bones’ position curves (though we may want rotation/scale at some future point?), and decodes the permutation data to add some extras.

Here’s the overview of that.  For details, I can offer no advice besides digging into the AnimationAdapter.cs code.  Sorry.  It’s pretty bad.
* First, it notes every ‘Perm’ bone keyframe.  It converts the coordinates into a permutation.
* For every such permutation frame, it adds a few things into the animation a small amount (currently 1/10th of a regular-speed frame) of time *before* the actual frame of the change.
    * When a bone is about to have its position taken over by a second, the first gets an extra key with the second’s ‘future’ position.  That way it’ll continue to move in its original trajectory for longer (9/10ths of a frame).
	* An ‘animation event’ is added.  This is when animations call functions from the object.  Functions must be on the same object that has the Animator that’s playing the animations, so for us that’s the Model object.  Look to ClustAnEve.cs.

### Swaps

Finally, the spheres (Sphere.cs) themselves get told to do a ‘swap’.  This consists of detaching from their parent (a bone from the armature), potentially floating unassisted toward the ‘future position’ where they’re supposed to meet their new parent bone, and then parenting themselves to said parent.

I don’t have anything foolproof here; the time steps can fail to align with the 1/60th-second frames of the clip, and so even at regular speed a frame might execute in the middle of the transition period.  The Animation Adapter does at least add constant interpolation to the added keyframe so that the bones snap to their new positions.  There’s probably room for improvement.
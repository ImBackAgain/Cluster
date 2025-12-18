# A little lost?

There’s a lot going here between the Blender file and the custom Editor scripting that you’ve done in Unity.

## Overview

You create files in Blender and export them as FBX files with [certain settings](#Export settings).  In Unity, your custom script mangles the animation clips to create copies that have a few extra bits and don’t have a few unnecessary bits.  These then have to be dragged into the Animator Controller, but we may be able to automate that with a bit more scripting.

## In Blender

### Animating basics

You have a number of objects using the same ‘Points’ armature, whose names all end in ‘Points’.  This is so the bones can have different Bone Constraints on them.  I expect to animate using a large number of curves, and I don’t want a skyscraper of different Follow Curve constraints on every bone all the time.

There’s also the one articulated armature called ‘Articulations’ (object ‘Artic’), and the Points Artic object (and probably all the others too) have Copy Location constraints to sit on the vertices of this skeleton.  For relevant animations, you may animate Artic while keeping the constraints’ Influnence at 1 to make the Points move articulatedly.

**Cyclic animations need to have ‘Cyc’ on the end**.  Okay, it’s not *vital*; you can make them cyclic in Unity manually after [wrangling them](#Animation Adapter).  But for consistency, add the ‘Cyc’.

## Animating permutations

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
    * Uncheck ‘All Actions’ and check ‘NLA Strips’.  Too many things get out with ‘all actions’.

## Unity

The files should be put into ‘Assets/FBX Imports’ to get them processed by the

### Animation Adapter

This fights the animation clips contained in our FBX files.  It creates copies of the animation clips which, hopefully, have all the properties we need.  The main code for this is in ‘Assets/Editor Scripting’ currently.

Oh, right.  It’s a Window.  From Unity’s menu bar, go to Windows → UI Toolkit → AnimAdapter.  You should see a little thingy with three text fields and a few buttons.  Of the text fields, the second is optional, and should be unnecessary as long as you use the export settings I mentioned).  Of the buttons, only the first is for real work; the other two are dummy buttons which I made to test random functions.

Here’s what the text inputs are for.

* The first is the file name, without the .fbx extension.
* The second is an ‘object name’; if Blender exports too many things, it may try to store the name of the relevant object in the name of the clip, like ‘Points Artic|Punch’.  We don’t want to put the whole thing in the clip name (below) because that also specifies the output file name, which can’t have pipes.
* The third is the name of the clip to fight.  It will be copied into a standalone file with the same as the clip (plus ‘.anim’).

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
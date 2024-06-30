# GRFON Examples

This directory contains several example GRFON files.  They are presented inline here, with commentary pointing out the GRFON features in use; and as separate .grfon files, suitable for testing your own GRFON parser.

## Monument Mod

This GRFON file is part of a mod for High Frontier.  It defines a monument building.  Things to note in this example:

- Comments (starting with //) for use by the human reader of the file.
- Whitespace is ignored, and can be used (for example) to make the values line up neatly.
- No quotation marks are needed around strings.
- Most key/value pairs are separated simply by line breaks; no extra punctuation is needed.  But you can use a semicolon to separate multiple items on one line, as shown with the "cost" item.
- Numbers can include decimal points or not, as needed.
- The model position is represented with three numbers, separated by a space.  (This is a convention adopted by High Frontier and not imposed by GRFON.)
- Line breaks (see info desc at the bottom of the file) are represented here with \n; this too is something supported by the game, passed through as-is by GRFON.

```
// This mod creates a monument which can be manually added to your city.
title: Founder Monument
type: building
subtype: manual decoration
author: {
    name:   Joe Strout
    email:  support@highfrontier.com
    www:    http://HighFrontier.com
}
model: {
    geometry: Monument.obj
    texture: Monument-Diffuse.jpg
    scale: 0.01
    position: 0 0 0
}
radius: 10
icon: ButtonIcon.png
cost: { initial: 0; perTurn: 0 }
limit: 1
effect: {
    radius: 100
    happiness: +10
}
info: {
    mouseover: Monument
    title: Honoring Bill T. Cat
    desc: Visionary, pioneer, and\nhonored founder whose\ncontribution will always\nbe remembered.
}
```

## Squawk Test

This example is a "squawk mod" for High Frontier.  It contains several key/value pairs (title, type, author, etc.), as well as an unkeyed list of collections (just one in this case) which contain the actual squawk data.

Notice how the "author" key maps to a sub-collection, which itself contains key/value pairs for name, email, and ww​w.

Also note that, even though colons and `//` have special meaning in GRFON, these don't need to be backslash-escaped when they occur together, as in the URL shown here.

This file also demonstrates how comments and blank lines can be used to make a file more comprehensible.

```
// First test of a squawk mod
title: Squawk Test 1
type: squawk
author: {
    name:    Joe Strout
    email:   support@highfrontier.com
    www:     http://HighFrontier.com
}
context: MainMenu

// Now follow batches of squawks.  Each batch is an unnamed collection
// containing:
//        when: a condition, or list of conditions
//        urgency: a value from 0 to 1
//        squawks: a list of actual squawks; each a string,
//            or a collection containing text, icon, and/or user.
// In a MainMenu squawk mod, there's not much point in having more
// than one batch.  But, that batch can have multiple squawks to
// choose from.

{
    when: RandomPercent < 50
    squawks: {
        This is a custom squawk!
        Mods rock!
        Making mods is super fun!
        { text: Is this mod on?; icon: FaceWorried; user: ModMan }
    }
}
```

## Project List

This file defines a set of "projects" (much like a tech tree) for the High Frontier game.  It has a few key/value pairs of general information at the top, but consists mainly of a list of collections, each defining one project.  Notice how easily further levels are included, such as the "prereqs" list under some of the project.

This also illustrates another common GRFON convention: any data that is not needed is simply omitted — so, for projects without any prerequisites, we haven't bothered to include the "prereqs" key at all.

```
// Built-In Project Nodes
title: Standard Projects
type: project
author: {
    name:   Strout & Sons
    email:  support@highfrontier.com
    www:    http://HighFrontier.com
}

{   shortName: FuelDepot
    displayName: Fuel Depot
    description: An orbital fuel depot enables buying and selling of fuel in orbit.  This opens new business opportunities, and can resupply larger facilities in cislunar space.
    xpCost: 5
}

{   shortName: NEARetrieval
    displayName: NEA Retrieval
    benefit: LowerCost
    description: The first retrieval of a Near Earth Asteroid provides a cache of valuable materials in orbit, and provides experience in manipulating large orbits in space.
    xpCost: 5
}

{   shortName: SSP
    displayName: Space Solar Power
    benefit: NewPart
    description: A space solar power station captures the abundant free energy of the Sun, and can beam it to customers anywhere in cislunar space.  The recipient needs only a simple microwave rectenna to receive steady, low-cost electricity.
    xpCost: 5
}

{   shortName: NukePower
    displayName: Nuclear Power
    description: Fission plants aren't new, but only a few have been flown in space, and those were very small.  Somebody's got to build a big one — and once it's been done once, we'll know how to power our cities with them!
    benefit: NewPart
    xpCost: 3
}

{   shortName: L1Station
    displayName: L1 Station
    benefit: NewOrbit
    description: A space station at the Earth-Moon L1 point is always in view of both Earth and Moon, and easy to reach from either.  It forms a critical transfer point for people, goods, and materials throughout cislunar space.
    prereqs: { FuelDepot; SSP }
    xpCost: 5
}

{   shortName: GasGun
    displayName: Gas Gun Launcher
    benefit: LowerCost
    description: Basically a mountain-sized gun for launching things from Earth into space, a gas gun would squish passengers into a thin smear on the back wall... but it's great for launching bulk materials very cheaply.
    prereqs: { FuelDepot }
    xpCost: 7
}
```

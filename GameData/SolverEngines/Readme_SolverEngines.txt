SolverEngines is by NathanKell and blowfish, based on the work of camlost for AJE.

License: LGPL.
GitHub (source): https://github.com/KSP-RO/SolverEngines

Installation:
Merge the zip's GameData folder with your KSP/GameData folder. After installing, inside KSP/GameData should be a SolverEngines folder (and inside that, Icons and Plugins folders).
Note: **Unless a mod you are using requires this, do not install it. It's for developers, not end users.**

SolverEngines is at its heart a replacement paradigm for how KSP deals with engines, splitting engines into a partmodule and an engine solver. Instead of a single engine module that does everything, or that is specifically geared to a single type of enigne, and instead of needing to keep a stock engine module present but then override its values every tick, SolverEngines decouples the engine module from the code that handles performance (the solver).

SolverEngines consists of five parts: the engine module and the engine solver, a replacement for the intake module that is geared to work with SolverEngines, a GUI for various flight and engine stats, and a replacement for ModuleAnimateHeat. The engine module derives from ModuleEnignesFX (and therefore is visible to MechJeb and KER), and handles all direct KSP interaction. The solver gets input data, and exposes various public methods for the module to get information back. The intake module derives from ModuleResourceIntake.

ModuleAnimateEmissive replaces ModuleAnimateHeat with a more flexible, configurable module. It can either be linked to a part's temperature (the default) or be told what state it should be each frame (so SolverEngines can control emissive animations directly).

**NOTE**: With the exception of ModuleAnimateEmissive, SolverEngines is **not a mod for end-users**. It is a mod for developers, who should derive classes from ModuleEnginesSolver and EngineSolver (as AJE and RealFuels do). It will do nothing on its own.

To use SolverEngines, implement two classes: a class that derives from EngineSolver that handles all calculation of thrust, Isp, and fuel flow given the passed-in parameters (and any new ones necessary), and a class that derives from ModuleEnginesSolver and overrides CreateEngine() to create an engine solver of the new type (and passes it any creation stats). Also override the info methods so proper info is displayed. You may also need to override other virtual methods in ModuleEnginesSolver (like UpdateFlightConditions and UpdateThrottle) depending on your engine's need for more status information or other requirements, and methods like OnStart or OnLoad to deal with more complexity.

Note that SolverEngines uses the [KSPAssembly] tag. Add this line to your AssemblyInfo.cs file to make KSP aware that your assembly depends on SolverEngines:
[assembly: KSPAssemblyDependency("SolverEngines", 1, 0)]
(replacing the two numbers with the major and minor version numbers of the SolverEngine release you are compiling against)

SolverEngines will also automatically create overheat bars if engineTemp approaches maxEngineTemp, and will set all ModuleAnimateEmissive modules on the part to solver.GetEmissive() each tick.

SolverEngines includes a GUI to display useful information about engines in flight, and additional info about air-breathing engines using SolverEngines when they are present.  The GUI will display an icon on blizzy87's toolbar if available, or on the stock toolbar if it is not.  All fields in the display GUI can be disabled in the settings window, and the display units can be changed in the units window.

See AJE or RealFuels for examples of how to implement SolverEngines in practice.

Changelog:

v3.6.2

* Recompile for KSP 1.4.3

v3.6.1

* Recompile for KSP 1.4.2

v3.6

* Recompile for KSP 1.4.1

v3.5

* Engine modules now support `THRUST_TRANSFORM` nodes to allow thrust transforms with multiple names
  * If present, these will overwrite any other thrust transform setup
  * Any number of these nodes can exist on the engine module
  * They have the following fields
    * `name`: name of the transforms(s) to be used (required)
    * `overallMultiplier`: thrust multiplier applied to all transforms with this name (optional)
    * `multiplier`: multiplier applied to individual transforms, there can be either 0, 1, or n multipliers, where n is the number of thrust transforms with this name
      * If there is 1 `multiplier` then it will be multiplied into `overallMultiplier`

v3.4

* Recompile for KSP 1.3.1

v3.3

* Recompile for KSP 1.3

v3.2

* Fix engines not allowing restart after running out of fuel and then fuel becoming available again

v3.1

* Fix a bug with engine fitting where results would persist after data was deleted
* Fix normalizedOutput to avoid phantom EC generation
* Prevent NRE from the overheat box on a non-active vessel
* Add vacuum conditions initializer for EngineThermodynamics
* Allow animations on engines that don't inherit from ModuleEnginesSolver (mainly to take better advantage of interfaces)

v3.0

* Update for KSP 1.2.2
* Fix toolbar button not displaying
* Fix thrust and drag displays in GUI
* Fix fittable engines being fit every time even if they had no changes
* **Breaking Change** Refactor EngineThermodynamics to be a struct, which makes it easier to use without adding GC alloc
* Fix issue with negative mach transformations
* **Breaking Change** Streamline engine fitting code
* Only save fitted engine when parsing prefab, this prevents part upgrades from affecting the cache (however, upgraded parts will have to be fit every time)

v2.3
* Revert AssemblyVersion to 2.1.0.0 since latest releases of AJE and RF depend on it

v2.2
* Recompile against KSP 1.1.3

v2.1
* Update for KSP 1.1.2

v2.0
* Updated for KSP 1.1
* Support variable thrust per transform as in ModuleEngines

v1.15
* Add some math utils
* Adjust UpdateFlightCondition to better accommodate simulations
* Add a generic animation class

v1.14
* Minor refactor of engine fitting
* Get rid of debug spam for non Earth/Kerbin atmospheres

v1.13
* Respect the "Ignore Max Temperature" cheat option.

v1.12
* Update to KSP 1.0.5.
* Get rid of ModuleAnimateEmissive, now unneded with stock changes.
* Remove engine code now included in stock (e.g. remove hacky event replacements).
* Make engine module and solver abstract.
* Allow engine modules to check whether they are underwater

v1.11
* Make heat production quadratic with throttle rather than linear.

v1.10
* Fix some NREs.
* Fix flickering of FX on burnout.
* Fix error in speed of sound formula; this fixes AJE propellers (with AJE 2.4+)
* Add hook for temperature-based autorestart.
* Make displayed actual throttle display two decimal points.

v1.9
* Prevent flight GUI button from disappearing.
* Fix flameouts at 0 mass requested.
* Fix a math bug.
* Add virtuals vFlameout and vUnflameout if a mod wants to override them.
* Allow required intake area to be adjusted.
* Cut TPR if insufficient intake area.
* Auto-unflameout thanks to nimaroth
* Fix issue with alternators.
* Fix an issue with engine fitting.

v1.8
* Fix for GUI issues in 1.7.
* To avoid the "can't restart engine once propellant is provided again" issue, shutdown and then activate your engine. That will clear its memory of flameouts.

v1.7
* Remove duplicate GUI entries.
* Avoid some NREs.

v1.6
* Finally fix the "can't start when shielded" thing. Activate/toggle was fixed but staging wasn't.
* Improve fitting code.
* Fix version file

v1.5
* EngineThermodynamics improvements (docs, track mass flow, allow mixing streams).
* Add functionality to fit an engine's performance parameters to a set of database parameters.
* Add SFC as a base solver member (in force/weight-hr).

v1.4
* Make the "can't start engine in fairing/bay" functionality toggleable ( noShieldedStart in the MODULE), and default to off.
* Speed things up a little in the GUI/VesselModule
* Changed GUI namespace to avoid an issue with Toolbar.

v1.3
* Pass velocity as a Vector3d (vel in the solver is still a double).

v1.2
* Made base members of the solver public for accessibility.
* Added Q (dynamic pressure) as one of the base members.

v1.1
* Allow easier configuration of temperature gauge
* Fix for flameout-at-zero-thrust

v1.0
Initial release

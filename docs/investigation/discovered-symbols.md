# Discovered Symbols

## Sources Used

- `Everything is Crab_Data/RuntimeInitializeOnLoads.json`
- `Everything is Crab_Data/ScriptingAssemblies.json`

## Runtime Initialization Entries Confirmed

Observed initialization entries include the following classes/methods:

- `BehaviorDesigner.Runtime.Behavior.DomainReset`
- `BehaviorDesigner.Runtime.BehaviorManager.DomainReset`
- `BehaviorDesigner.Runtime.GlobalVariables.DomainReset`
- `Sirenix.Serialization.UnitySerializationInitializer.InitializeRuntime`
- `Sirenix.Serialization.Utilities.UnityVersion.EnsureLoaded`
- `Sirenix.Utilities.UnityVersion.EnsureLoaded`
- `Config.Initialize`
- `ProgressManager.InitializeStatics`
- `PlatformSupport.PlatformSupportController.InitOnPlayMode`
- `GameFlow.GameFlowController.InitializeRunInBackground`
- `World.WorldEvents.ResetTriggerWhenReadyEventsRelatedToRun`
- `MoreMountains.Feedbacks.MMFeedbacksEvent.RuntimeInitialization`

These names are useful as anchor points for future hook discovery and boot timing.

## Assembly and Package Signals

- Core gameplay assembly signal: `Assembly-CSharp`
- Behavior Designer runtime signal: `BehaviorDesigner.Runtime`
- Odin/Sirenix signal: `Sirenix.Serialization`, `Sirenix.Utilities`
- Unity module list indicates broad Unity 6 module set and localization/audio/content systems.

## Notes

- This file tracks confirmed symbol names only.
- Add new candidate hook points with scene/context notes as runtime exploration continues.

// FILE: netlogic.core/Sim/Systems/PreserveHandlersAssembly.cs
// This makes ALL types in this assembly roots for UnityLinker, preventing IL2CPP stripping.
// Use this if you rely on reflection-based auto-discovery of handler types.

#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;

// IMPORTANT:
// Assembly-level Preserve keeps all types in this assembly (as if you put [Preserve] on each type).
// This is the "automatic" solution for reflection-discovered types in IL2CPP builds.
[assembly: Preserve]
#endif

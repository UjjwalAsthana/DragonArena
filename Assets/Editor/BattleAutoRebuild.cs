using System.IO;
using UnityEditor;
using UnityEngine;

namespace DragonBattle.EditorTools
{
    /// <summary>
    /// Rebuilds the battle scene once after this script is first compiled, so the new dragons/UI
    /// show up without having to find the menu. Delete this file after it has run.
    /// </summary>
    [InitializeOnLoad]
    public static class BattleAutoRebuild
    {
        private const string Marker = "Library/BattleAutoRebuild_v1.done";

        static BattleAutoRebuild()
        {
            if (File.Exists(Marker)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying || EditorApplication.isCompiling || File.Exists(Marker)) return;
                File.WriteAllText(Marker, "done");
                BattleSceneBuilder.Build();
            };
        }
    }
}

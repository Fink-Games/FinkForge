using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FinkForge.Editor
{
    // Makes a developer-provided .pem file a normal TextAsset while preserving
    // the real PEM filename and format in the Unity project.
    [ScriptedImporter(1, "pem")]
    internal sealed class FinkPemImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var pem = File.ReadAllText(context.assetPath);
            var textAsset = new TextAsset(pem);
            context.AddObjectToAsset("pem", textAsset);
            context.SetMainObject(textAsset);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行 / 编辑器菜单共用的构建入口。
/// 命令行用法：
/// Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt; -executeMethod BuildScript.BuildWindows64 -logFile &lt;日志路径&gt;
/// </summary>
public static class BuildScript
{
    private const string OutputDir = "build";
    private const string ExeName = "ThreeKingdomsCardGame.exe";

    [MenuItem("Build/Windows 64")]
    public static void BuildWindows64()
    {
        string[] scenes = GetEnabledScenes();

        if (scenes.Length == 0)
        {
            throw new Exception("EditorBuildSettings 中没有启用的场景，无法构建");
        }

        Debug.Log("[BuildScript] 参与构建的场景数量：" + scenes.Length);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log(
            "[BuildScript] 结果=" + summary.result
            + " 耗时=" + summary.totalTime
            + " 大小=" + summary.totalSize
            + " 错误数=" + summary.totalErrors
            + " 输出=" + summary.outputPath
        );

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception(
                "构建失败：" + summary.result + "，错误数 " + summary.totalErrors
            );
        }
    }

    private static string[] GetEnabledScenes()
    {
        List<string> list = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene != null && scene.enabled)
            {
                list.Add(scene.path);
            }
        }

        return list.ToArray();
    }
}

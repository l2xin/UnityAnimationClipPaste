using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using System.IO;
using UnityEditor.Animations;

public class AnimationClipCopy : MonoBehaviour
{
    private static int s_countChange = 0;


    [MenuItem("Assets/遍历替换AnimatorControllerMotion")]
    static void CheckSceneSetting()
    {
        Pack(Application.dataPath + "/14_Leishen_2");
    }

    static void Pack(string source)
    {
        DirectoryInfo folder = new DirectoryInfo(source);
        FileSystemInfo[] files = folder.GetFileSystemInfos();

        List<string> curChildFolderNameArr = new List<string>();
        List<string> curAnimationNameArr = new List<string>();
        List<string> curAnimatorControllerNameArr = new List<string>() ;

        int length = files.Length;
        for (int i = 0; i < length; i++)
        {
            string fileFullName = files[i].FullName;
            if (files[i] is DirectoryInfo)
            {
                curChildFolderNameArr.Add(fileFullName);
            }
            else
            {
                if (files[i].Name.ToLower().EndsWith(".fbx"))
                {
                    curAnimationNameArr.Add(fileFullName);
                }
                else if(files[i].Name.ToLower().EndsWith(".controller"))
                {
                    curAnimatorControllerNameArr.Add(fileFullName);
                }
            }
        }

        s_curAnimationClipArr = new List<AnimationClip>();

        for (int i = 0; i < curAnimatorControllerNameArr.Count; i++)
        {
            string curAnimatorControllerPath = curAnimatorControllerNameArr[i].Substring(Application.dataPath.Length - 6).Replace("\\", "/");
            SaveAnimatorControllerMotionDict(curAnimatorControllerPath);

            EditorUtility.DisplayProgressBar("保存动作控制器AnimatorController", "" + curAnimatorControllerPath, (float)((float)i / curAnimatorControllerNameArr.Count));
        }

        for (int i = 0; i < curAnimationNameArr.Count; i++)
        {
            string fbxPath = curAnimationNameArr[i].Substring(Application.dataPath.Length - 6).Replace("\\", "/");
            string parentPath = GetParentPathForAsset(fbxPath);
            string animationName = curAnimationNameArr[i].Split('@')[1].Replace(".FBX", "");
            AnimCopy(fbxPath, parentPath, animationName);

            EditorUtility.DisplayProgressBar("生成动画文件clip", "" + fbxPath, (float)((float)i / curAnimationNameArr.Count));
        }

        for (int i = 0; i < curAnimatorControllerNameArr.Count; i++)
        {
            string curAnimatorControllerPath = curAnimatorControllerNameArr[i].Substring(Application.dataPath.Length - 6).Replace("\\", "/");
            AnimatorLink(curAnimatorControllerPath);

            EditorUtility.DisplayProgressBar("连接动作控制器", "" + curAnimatorControllerPath, (float)((float)i / curAnimatorControllerNameArr.Count));
        }

        for (int i = 0; i < curChildFolderNameArr.Count; i++)
        {
            Pack(curChildFolderNameArr[i]);
        }

        EditorUtility.ClearProgressBar();

        AssetDatabase.SaveAssets();//保存修改
        AssetDatabase.Refresh();
    }


#region 保存动画控制器关联关系

    //animatorControllerName stateName motionName
    private static Dictionary<string, Dictionary<string, string>> s_animatorControllerMotionDict;

    private static string GetMotionName(string animatorControllerPath, string stateName)
    {
        if(s_animatorControllerMotionDict == null)
        {
            return "";
        }
        var dict = s_animatorControllerMotionDict[animatorControllerPath];
        if(dict == null)
        {
            return "";
        }

        string motionName;
        if(dict.TryGetValue(stateName, out motionName))
        {
            return motionName;
        }
        return "";
    }

    //存储animatorControllerName-<stateName,motionName>
    private static void SaveAnimatorControllerMotionDict(string animatorControllerPath)
    {
        if(s_animatorControllerMotionDict == null)
        {
            s_animatorControllerMotionDict = new Dictionary<string, Dictionary<string, string>>();
        }

        s_animatorControllerMotionDict[animatorControllerPath] = new Dictionary<string, string>();
        AnimatorController animatorController = AssetDatabase.LoadAssetAtPath(animatorControllerPath, typeof(AnimatorController)) as AnimatorController;
        for (int i = 0; i < animatorController.layers.Length; i++)
        {
            AnimatorControllerLayer layer = animatorController.layers[i];
            SaveAnimatorControllerMotion(animatorControllerPath, layer.stateMachine, animatorController);
        }
    }


    static void SaveAnimatorControllerMotion(string animatorControllerPath, AnimatorStateMachine stateMachine, AnimatorController animatorController)
    {
        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            ChildAnimatorState childState = stateMachine.states[i];
            if (childState.state.motion == null)
            {
                if (childState.state.name.CompareTo("New State") == 0)
                    continue;

                continue;
            }
            if (childState.state.motion.GetType() == typeof(AnimationClip))
            {
                var motionNameDict = s_animatorControllerMotionDict[animatorControllerPath];
                string stateName = childState.state.name;
                string motionName = childState.state.motion.name;
                if (motionNameDict.ContainsKey(stateName))
                {
                    motionNameDict[stateName] = motionName;
                }
                else
                {
                    motionNameDict.Add(stateName, motionName);
                }
            }

#if BlendTree 
            else if (childState.state.motion.GetType() == typeof(BlendTree))
            {

                //BlendTree这个类有BUG，不能直接修改Motion, 要先记录原本的信息，再全部删除原本的，再修改，再加上去.

                List<Motion> allMotion = new List<Motion>();
                List<float> allThreshold = new List<float>();
                BlendTree tree = (BlendTree)childState.state.motion;

                for (int k = 0; k < tree.children.Length; k++)
                {
                    allMotion.Add(tree.children[k].motion);
                    allThreshold.Add(tree.children[k].threshold);
                }

                for (int k = 0; k < allMotion.Count; k++)
                {
                    if (allMotion[k].GetType() == typeof(AnimationClip))
                    {
                        for (int j = 0; j < newClips.Length; j++)
                        {
                            if (newClips[j].name.CompareTo(allMotion[k].name) == 0)
                            {
                                allMotion[k] = (Motion)newClips[j];
                                s_countChange++;
                                break;
                            }
                        }
                    }
                    else if (allMotion[k].GetType() == typeof(BlendTree))
                    {
                        Debug.LogError("You need to change it!");
                    }
                }

                for (int k = tree.children.Length - 1; k >= 0; k--)
                {
                    tree.RemoveChild(k);
                }

                for (int k = 0; k < allMotion.Count; k++)
                {
                    tree.AddChild(allMotion[k], allThreshold[k]);
                }

            }
#endif
        }

        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            SaveAnimatorControllerMotion(animatorControllerPath, stateMachine.stateMachines[i].stateMachine, animatorController);
        }
    }

    #endregion


#region 替换motion
    private static void AnimatorLink(string animatorControllerPath)
    {
        AnimatorController animatorController = AssetDatabase.LoadAssetAtPath(animatorControllerPath, typeof(AnimatorController)) as AnimatorController;
        Debug.Log("animatorController.name:" + animatorController.name);
        for(int i = 0; i< animatorController.layers.Length; i++)
        {
            AnimatorControllerLayer layer = animatorController.layers[i];
            CheckAndRefreshAnimatorController(animatorControllerPath, s_curAnimationClipArr.ToArray(), layer.stateMachine, animatorController);
        }
    }


    static void CheckAndRefreshAnimatorController(string animatorControllerPath, AnimationClip[] newClips, AnimatorStateMachine stateMachine, AnimatorController animatorController)
    {
        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            ChildAnimatorState childState = stateMachine.states[i];

            string motionName = GetMotionName(animatorControllerPath, childState.state.name);
            if(!string.IsNullOrEmpty(motionName))
            {
                for (int j = 0; j < newClips.Length; j++)
                {
                    if (newClips[j].name.CompareTo(motionName) == 0)
                    {
                        childState.state.motion = (Motion)newClips[j];
                        Debug.Log("替换成功 animatorController:" + animatorController.name + " motionName:" + motionName);
                        s_countChange++;
                        break;
                    }
                }
            }
            else
            {
                if (childState.state.motion == null)
                {
                    if (childState.state.name.CompareTo("New State") == 0)
                        continue;

                    Debug.LogError("替换失败 animatorController:" + animatorController.name + " Null : " + childState.state.name);
                    continue;
                }
                if (childState.state.motion.GetType() == typeof(AnimationClip))
                {
                    for (int j = 0; j < newClips.Length; j++)
                    {
                        if (newClips[j].name.CompareTo(childState.state.motion.name) == 0)
                        {
                            childState.state.motion = (Motion)newClips[j];
                            Debug.Log("替换成功 animatorController:" + animatorController.name + "childState.state.name:" + childState.state.name);
                            s_countChange++;
                            break;
                        }
                    }
                }
#if BlendTree
                else if (childState.state.motion.GetType() == typeof(BlendTree))
                {

                    //BlendTree这个类有BUG，不能直接修改Motion, 要先记录原本的信息，再全部删除原本的，再修改，再加上去.

                    List<Motion> allMotion = new List<Motion>();
                    List<float> allThreshold = new List<float>();
                    BlendTree tree = (BlendTree)childState.state.motion;

                    for (int k = 0; k < tree.children.Length; k++)
                    {
                        allMotion.Add(tree.children[k].motion);
                        allThreshold.Add(tree.children[k].threshold);
                    }

                    for (int k = 0; k < allMotion.Count; k++)
                    {
                        if (allMotion[k].GetType() == typeof(AnimationClip))
                        {
                            for (int j = 0; j < newClips.Length; j++)
                            {
                                if (newClips[j].name.CompareTo(allMotion[k].name) == 0)
                                {
                                    allMotion[k] = (Motion)newClips[j];
                                    s_countChange++;
                                    break;
                                }
                            }
                        }
                        else if (allMotion[k].GetType() == typeof(BlendTree))
                        {
                            Debug.LogError("You need to change it!");
                        }
                    }

                    for (int k = tree.children.Length - 1; k >= 0; k--)
                    {
                        tree.RemoveChild(k);
                    }

                    for (int k = 0; k < allMotion.Count; k++)
                    {
                        tree.AddChild(allMotion[k], allThreshold[k]);
                    }

                }
#endif
            }
        }



        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            CheckAndRefreshAnimatorController(animatorControllerPath, newClips, stateMachine.stateMachines[i].stateMachine, animatorController);
        }
    }

    #endregion


#region 拷贝动画clip

    /// <summary>
    /// 返回传入目录的父目录(相对于asset)
    /// </summary>
    /// <param name="assetPath"></param>
    /// <returns></returns>
    private static string GetParentPathForAsset(string assetPath)
    {
        string[] pathName = assetPath.Split('/');
        string parentPath = "";

        if (pathName.Length < 2 || pathName[pathName.Length - 1] == "")
        {
            //Debug.Log(assetPath + @"没有父目录！");
            return parentPath;
        }

        for (int i = 0; i < pathName.Length - 1; i++)
        {

            if (i != pathName.Length - 2)
                parentPath += pathName[i] + @"/";
            else
                parentPath += pathName[i];
        }

        return parentPath;
    }

    private static List<AnimationClip>  s_curAnimationClipArr;

    private static void AnimCopy(string fbxPath, string parentPath, string name)
    {
        Object[] objs = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        //Debug.Log("fbxPath:" + fbxPath + " parentPath:" + parentPath);

        string animationPath = "";

        AnimationClipSettings setting;

        AnimationClip srcClip;//源AnimationClip

        AnimationClip newClip;//新AnimationClip

        foreach (Object o in objs)
        {
            if (o.GetType() == typeof(AnimationClip) && o.name == name)
            {
                srcClip = o as AnimationClip;

                newClip = new AnimationClip();

                newClip.name = srcClip.name;//设置新clip的名字

                s_curAnimationClipArr.Add(newClip);

                //if (!Directory.Exists(parentPath + @"/copy/"))
                //{
                //    Directory.CreateDirectory(parentPath + @"/copy/");
                //}

                //animationPath = parentPath + @"/copy/" + newClip.name + ".anim";

                animationPath = parentPath + @"/" + newClip.name + ".anim";

                setting = AnimationUtility.GetAnimationClipSettings(srcClip);//获取AnimationClipSettings

                AnimationUtility.SetAnimationClipSettings(newClip, setting);//设置新clip的AnimationClipSettings

                newClip.frameRate = srcClip.frameRate;//设置新clip的帧率

                EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(srcClip);//获取clip的curveBinds

                for (int i = 0; i < curveBindings.Length; i++)
                {
                    AnimationUtility.SetEditorCurve(newClip, curveBindings[i], AnimationUtility.GetEditorCurve(srcClip, curveBindings[i]));//设置新clip的curve
                }
                
                AssetDatabase.CreateAsset(newClip, animationPath); //AssetDatabase中的路径都是相对Asset的  如果指定路径已存在asset则会被删除，然后创建新的asset
            }
        }
    }

#endregion

    //[MenuItem("Assets/CopyAnimationClip")]
    //private static void AnimationClipsCopy()
    //{
    //    Object[] go = Selection.objects;
    //    string Path = AssetDatabase.GetAssetPath(go[0]);
    //    string parentPath = GetParentPathForAsset(Path);
    //    s_curAnimationClipArr = new List<AnimationClip>();

    //    for (int i = 0; i < go.Length; i++)
    //    {
    //        string fbxPath = AssetDatabase.GetAssetPath(go[i]);
    //        AnimCopy(fbxPath, parentPath, go[i].name.Split('@')[1]);
    //    }

    //    AssetDatabase.SaveAssets();//保存修改

    //    AssetDatabase.Refresh();
    //}


    //[MenuItem("Assets/AnimatorLink")]
    //private static void AnimatorLinkItem()
    //{
    //    Object[] go = Selection.objects;
    //    string Path = AssetDatabase.GetAssetPath(go[0]);
    //    for (int i = 0; i < go.Length; i++)
    //    {
    //        string fbxPath = AssetDatabase.GetAssetPath(go[i]);
    //        AnimatorLink(fbxPath);
    //    }
    //}
}

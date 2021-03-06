using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using System.Linq;

namespace LookDev.Editor
{
    internal static class SearchProviderForModels
    {
        internal static string id = "LookDev_Model";
        internal static string name = "Model";

        public static List<string> folders = new List<string>();
        public static List<string> objectsGUID = new List<string>();

        static readonly string defaultLookdevFolder = "Assets/LookDev/Models";
        public static string defaultFolder = "Assets/LookDev/Models";

        public static bool showModel;
        public static bool showPrefab;

        static string[] results;
        static List<string> resultList = new List<string>();


        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            return new SearchProvider(id, name)
            {
                active = false,
                filterId = "mod:",
                priority = 12, // put example provider at a low priority
                fetchItems = (context, items, provider) =>
                {
                    if (ProjectSettingWindow.projectSetting != null)
                    {
                        string projectPath = ProjectSettingWindow.projectSetting.GetImportAssetPath();
                        if (string.IsNullOrEmpty(projectPath) == false)
                            defaultFolder = projectPath;
                        else
                            defaultFolder = defaultLookdevFolder;
                    }
                    else
                        defaultFolder = defaultLookdevFolder;

                    string filter = string.Empty;

                    if (showModel)
                        filter = filter + "t:Model ";
                    if (showPrefab)
                        filter = filter + "t:Prefab ";

                    resultList.Clear();

                    if (folders.Count == 0 && objectsGUID.Count == 0)
                    {
                        results = AssetDatabase.FindAssets($"{filter}" + context.searchQuery, new string[] { defaultFolder });
                        resultList = results.ToList<string>();
                        results.Initialize();
                    }
                    else
                    {
                        if (filter == string.Empty)
                            return null;

                        if (folders.Count != 0)
                        {
                            results = AssetDatabase.FindAssets($"{filter}" + context.searchQuery, folders.ToArray());
                            resultList = results.ToList<string>();
                            results.Initialize();
                        }

                        if (objectsGUID.Count != 0)
                        {
                            for (int i = 0; i < objectsGUID.Count; i++)
                            {
                                if (resultList.Contains(objectsGUID[i]) == false)
                                    resultList.Add(objectsGUID[i]);
                            }

                        }
                    }

                    foreach (var guid in resultList)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        var firstRenderer = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath).GetComponentInChildren<Renderer>();

                        bool foundAnimation = false;
                        Object[] subObjs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);

                        foreach(Object subObj in subObjs)
                        {
                            if (subObj.GetType() == typeof(AnimationClip))
                                foundAnimation = true;
                        }

                        // It's to hide a model which does not have renderers or Animation
                        // If the model just has only animation without having any renderers, the file will be deleted.
                        if (firstRenderer == null && foundAnimation == true)
                            continue;

                        if (firstRenderer == null && foundAnimation == false)
                            continue;
                        if (firstRenderer != null)
                            items.Add(provider.CreateItem(context, AssetDatabase.GUIDToAssetPath(guid), null, null, null, null));

                    }
                    return null;

                },
#pragma warning disable UNT0008 // Null propagation on Unity objects
                // Use fetch to load the asset asynchronously on display
                fetchThumbnail = (item, context) => AssetDatabase.GetCachedIcon(item.id) as Texture2D,
                fetchPreview = (item, context, size, options) => AssetPreview.GetAssetPreview(item.ToObject()) as Texture2D,
                fetchLabel = (item, context) => AssetDatabase.LoadMainAssetAtPath(item.id)?.name,
                fetchDescription = (item, context) => item.id,
                toObject = (item, type) => AssetDatabase.LoadMainAssetAtPath(item.id),
#pragma warning restore UNT0008 // Null propagation on Unity objects
                // Shows handled actions in the preview inspector
                // Shows inspector view in the preview inspector (uses toObject)
                showDetails = false,
                showDetailsOptions = ShowDetailsOptions.None,
                trackSelection = (item, context) =>
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(item.id);
                    if (obj != null)
                    {
                        if (context.selection.Count == 1)
                        {
                            //EditorGUIUtility.PingObject(obj.GetInstanceID());
                            Selection.activeInstanceID = obj.GetInstanceID();
                        }
                        else if (context.selection.Count > 1)
                        {
                            List<Object> objList = new List<Object>();
                            foreach (SearchItem sItem in context.selection)
                            {
                                var targetObj = AssetDatabase.LoadMainAssetAtPath(sItem.id);

                                if (targetObj != null)
                                    objList.Add(targetObj);
                            }
                            Selection.objects = objList.ToArray();
                        }
                    }
                },
                startDrag = (item, context) =>
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(item.id);
                    if (obj != null)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new Object[] { obj };

                        if (Selection.objects.Length > 1)
                        {
                            DragAndDrop.objectReferences = Selection.objects;
                        }

                        DragAndDrop.StartDrag(item.label);
                    }
                    
                }
            };
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            return new[]
            {
            new SearchAction(id, "Edit in Mesh Dcc", null, "Description")
            {
                handler = (item) =>
                {
                    switch(ProjectSettingWindow.projectSetting.meshDccs)
                    {
                        case MeshDCCs.Maya:
                            AssetManageHelpers.LoadModelOnDCC(item.id, DCCType.MAYA);
                            break;

                        case MeshDCCs.Max:
                            AssetManageHelpers.LoadModelOnDCC(item.id, DCCType.MAX);
                            break;

                        /*
                        case MeshDCCs.Blender:
                            AssetManageHelpers.LoadModelOnDCC(item.id, DCCType.BLENDER);
                            break;
                        */
                    }
                }
            },
            new SearchAction(id, "Edit in Mesh Painitng Dcc", null, "Description")
            {
                handler = (item) =>
                {
                    switch(ProjectSettingWindow.projectSetting.paintingMeshDccs)
                    {
                        case PaintingMeshDCCs.Substance_Painter:
                            AssetManageHelpers.LoadModelOnPaintingDCC(item.id, ProjectSettingWindow.projectSetting.paintingMeshDccs);
                            break;
                    }
                }
            },
            new SearchAction(id, "Show in Explorer", null, "Description")
            {
                handler = (item) =>
                {
                    AssetManageHelpers.ShowinExplorer(item.id);
                }
            },
            new SearchAction(id, "Go to Material", null, "Description")
            {
                handler = (item) =>
                {
                    AssetManageHelpers.GoToAsset(item.id);
                }
            },
            new SearchAction(id, "Duplicate Model", null, "Description")
            {
                handler = (item) =>
                {
                    AssetManageHelpers.DuplicateSelectedAssets();
                }
            },
            new SearchAction(id, "Rename Model", null, "Description")
            {
                handler = (item) =>
                {
                    AssetManageHelpers.RenameSelectedAssets();
                }
            },
            new SearchAction(id, "Delete Model", null, "Description")
            {
                handler = (item) =>
                {
                    AssetManageHelpers.DeleteSelectedAssets();
                }
            },
            new SearchAction(id, "Show related Assets", null, "Description")
            {
                handler = (item) =>
                {
                    LookDevSearchFilters lookDevSearchFilters = EditorWindow.GetWindow<LookDevSearchFilters>();

                    if (lookDevSearchFilters != null)
                    {
                        LookDevFilter instantFilter = new LookDevFilter();
                        instantFilter.enabled = true;
                        instantFilter.filterName = $"FROM_MODEL ({System.IO.Path.GetFileNameWithoutExtension(item.id)})";
                        instantFilter.objectGuid.Add(AssetDatabase.AssetPathToGUID(item.id));
                        instantFilter.showModel = true;
                        instantFilter.showPrefab = true;

                        LookDevSearchFilters.SaveFilter(instantFilter);

                        LookDevSearchFilters.RefreshFilters();

                        lookDevSearchFilters.OnRemoveAllFilters();

                        if (LookDevSearchFilters.filters.ContainsKey(instantFilter.filterName))
                            LookDevSearchFilters.filters[instantFilter.filterName].enabled = true;

                        lookDevSearchFilters.OnChangedFilters();
                    }
                }
            },
        };
        }

        /*
        public static void Init()
        {
            ISearchView searchView = SearchService.ShowContextual(id);
        }

        static void InitMaterialSearcherBytheProvider()
        {
            TestSearchService.SetAllSearchProvidersDisabled();
            Init();
        }

        public static void InitAllLookDevSearcher()
        {

            TestSearchService.SetAllSearchProvidersDisabled();
            ISearchView searchView = SearchService.ShowContextual("LookDev_Material", "LookDev_Texture", "LookDev_Model");

            Debug.Log(searchView.context.focusedWindow);
            Debug.Log(searchView.context.filterId);

        }
        */
    }
}


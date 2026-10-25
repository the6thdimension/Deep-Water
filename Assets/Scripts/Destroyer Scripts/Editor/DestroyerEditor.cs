using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DestroyerController))]
public class DestroyerEditor : Editor {

    private int spacing = 15;
    private int tab;
    
    private bool BBPos = false;
    private bool SPos = false;


    public override void OnInspectorGUI() {

        GUIStyle headStyle = new GUIStyle();
        headStyle.fontSize = 20; 
        headStyle.normal.textColor = Color.white;

        GUIStyle subHeadStyle = new GUIStyle();
        subHeadStyle.fontSize = 13; 
        subHeadStyle.normal.textColor = Color.white;


        int selected = 0;
        string[] options = new string[]
        {
            "CVN 78", "CVN 79", 
        };


        EditorGUILayout.LabelField("FDS - Ford Class destroyer Controller", headStyle);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


        
        tab = GUILayout.Toolbar (tab, new string[] {"General", "Ship Movement", "Flight Deck", "Hangar", "Misc."});
        
        DestroyerController destroyerController = (DestroyerController)target;

        if(tab == 0)
        {
            GUILayout.Space(spacing);

            EditorGUILayout.LabelField("Ship Settings", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            selected = EditorGUILayout.Popup("Ship Number", selected, options); 

            GUILayout.Space(spacing);

            EditorGUILayout.LabelField("Ocean Settings", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // OCEAN CODE
            // destroyerController.Ocean = (UnityEngine.Rendering.HighDefinition.WaterSurface)EditorGUILayout.ObjectField("Ocean System", destroyerController.Ocean, typeof(UnityEngine.Rendering.HighDefinition.WaterSurface), true);
            
            
            //destroyerController.
        }
        else if(tab == 1)
        {
            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Ship Sway", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            destroyerController.SwayHeightIntensity = EditorGUILayout.Slider("Sway Height Intensity", destroyerController.SwayHeightIntensity, 0.0f, 10.0f);
            destroyerController.SwayRollIntensity   = EditorGUILayout.Slider("Sway Roll Intensity", destroyerController.SwayRollIntensity, 0.0f, 10.0f);
            destroyerController.SwayPitchIntensity  = EditorGUILayout.Slider("Sway Pitch Intensity", destroyerController.SwayPitchIntensity, 0.0f, 10.0f);
            

            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Ship Movement", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            destroyerController.ShipSpeed   = EditorGUILayout.Slider("Ship Speed", destroyerController.ShipSpeed, 0.0f, 35.0f);
            destroyerController.ShipTurn    = EditorGUILayout.Slider("Ship Turn", destroyerController.ShipTurn, -8f, 8f);

        }
        else if(tab == 2)
        {

            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Catapults", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);



            EditorGUILayout.LabelField("Catapult - 1", subHeadStyle);

                if(destroyerController.Aircraft1 = (GameObject)EditorGUILayout.ObjectField("Aircraft 1", destroyerController.Aircraft1, typeof(GameObject), true))
                {
                    
                }

                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        destroyerController.RaiseBackBlast(destroyerController.BS1);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        destroyerController.LowerBackBlast(destroyerController.BS1);
                    }


                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    if(GUILayout.Button("Launch Cat 1!"))
                    {
                        destroyerController.LaunchCat(destroyerController.Shuttle1);
                    }
                GUILayout.EndHorizontal();



            EditorGUILayout.LabelField("Catapult - 2", subHeadStyle);

                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        destroyerController.RaiseBackBlast(destroyerController.BS2);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        destroyerController.LowerBackBlast(destroyerController.BS2);
                    }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    if(GUILayout.Button("Launch Cat 2!"))
                    {
                        Debug.Log("It's alive: " + target.name);
                    }
                GUILayout.EndHorizontal();
                


            EditorGUILayout.LabelField("Catapult - 3", subHeadStyle);

                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        destroyerController.RaiseBackBlast(destroyerController.BS3);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        destroyerController.LowerBackBlast(destroyerController.BS3);
                    }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    if(GUILayout.Button("Launch Cat 3!"))
                    {
                        Debug.Log("It's alive: " + target.name);
                    }
                GUILayout.EndHorizontal();


                EditorGUILayout.LabelField("Catapult - 4", subHeadStyle);


                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        destroyerController.RaiseBackBlast(destroyerController.BS4);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        destroyerController.LowerBackBlast(destroyerController.BS4);
                    }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    if(GUILayout.Button("Launch Cat 4!"))
                    {
                        Debug.Log("It's alive: " + target.name);
                    }
                GUILayout.EndHorizontal();

        }
        else if(tab == 3)
        {
            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Elevators", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Hangar Inventory", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            destroyerController.FA18E = (GameObject)EditorGUILayout.ObjectField("F18Prefab", destroyerController.FA18E, typeof(GameObject), true);

            EditorGUILayout.Separator();
            if(GUILayout.Button("Spawn F18"))
            {
                destroyerController.spawnFromHangar(0,0, destroyerController.FA18E);
            }
        }
        else if(tab == 4)
        {
            
            GUILayout.Space(spacing);


            EditorGUILayout.LabelField("GameObjects", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


            EditorGUILayout.LabelField("Catapult - BackBlast Shields");

                destroyerController.BS1 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 1", destroyerController.BS1, typeof(GameObject), true);
                destroyerController.BS2 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 2", destroyerController.BS2, typeof(GameObject), true);
                destroyerController.BS3 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 3", destroyerController.BS3, typeof(GameObject), true);
                destroyerController.BS4 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 4", destroyerController.BS4, typeof(GameObject), true);
            
            EditorGUILayout.LabelField("Catapult - Shuttles");

                destroyerController.Shuttle1 = (GameObject)EditorGUILayout.ObjectField("Shuttle 1", destroyerController.Shuttle1, typeof(GameObject), true);
                destroyerController.Shuttle2 = (GameObject)EditorGUILayout.ObjectField("Shuttle 2", destroyerController.Shuttle2, typeof(GameObject), true);
                destroyerController.Shuttle3 = (GameObject)EditorGUILayout.ObjectField("Shuttle 3", destroyerController.Shuttle3, typeof(GameObject), true);
                destroyerController.Shuttle4 = (GameObject)EditorGUILayout.ObjectField("Shuttle 4", destroyerController.Shuttle4, typeof(GameObject), true);  

            EditorGUILayout.LabelField("Elevators");

                destroyerController.Elevator1 = (Transform)EditorGUILayout.ObjectField("Elevator 1", destroyerController.Elevator1, typeof(Transform), true);
                destroyerController.Elevator2 = (Transform)EditorGUILayout.ObjectField("Elevator 2", destroyerController.Elevator2, typeof(Transform), true);
                destroyerController.Elevator3 = (Transform)EditorGUILayout.ObjectField("Elevator 3", destroyerController.Elevator3, typeof(Transform), true);


        }

       


        //base.OnInspectorGUI();


    }
}





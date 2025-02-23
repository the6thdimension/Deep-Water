using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CarrierController))]
public class CarrierEditor : Editor {

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


        EditorGUILayout.LabelField("FDS - Ford Class Carrier Controller", headStyle);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


        
        tab = GUILayout.Toolbar (tab, new string[] {"General", "Ship Movement", "Flight Deck", "Hangar", "Misc."});
        
        CarrierController carrierController = (CarrierController)target;

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
            // carrierController.Ocean = (UnityEngine.Rendering.HighDefinition.WaterSurface)EditorGUILayout.ObjectField("Ocean System", carrierController.Ocean, typeof(UnityEngine.Rendering.HighDefinition.WaterSurface), true);
            
            
            //carrierController.
        }
        else if(tab == 1)
        {
            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Ship Sway", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            carrierController.SwayHeightIntensity = EditorGUILayout.Slider("Sway Height Intensity", carrierController.SwayHeightIntensity, 0.0f, 10.0f);
            carrierController.SwayRollIntensity   = EditorGUILayout.Slider("Sway Roll Intensity", carrierController.SwayRollIntensity, 0.0f, 10.0f);
            carrierController.SwayPitchIntensity  = EditorGUILayout.Slider("Sway Pitch Intensity", carrierController.SwayPitchIntensity, 0.0f, 10.0f);
            

            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Ship Movement", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            carrierController.ShipSpeed   = EditorGUILayout.Slider("Ship Speed", carrierController.ShipSpeed, 0.0f, 35.0f);
            carrierController.ShipTurn    = EditorGUILayout.Slider("Ship Turn", carrierController.ShipTurn, -8f, 8f);

        }
        else if(tab == 2)
        {

            GUILayout.Space(spacing);
            EditorGUILayout.LabelField("Catapults", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);



            EditorGUILayout.LabelField("Catapult - 1", subHeadStyle);

                if(carrierController.Aircraft1 = (GameObject)EditorGUILayout.ObjectField("Aircraft 1", carrierController.Aircraft1, typeof(GameObject), true))
                {
                    
                }

                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        carrierController.RaiseBackBlast(carrierController.BS1);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        carrierController.LowerBackBlast(carrierController.BS1);
                    }


                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    if(GUILayout.Button("Launch Cat 1!"))
                    {
                        carrierController.LaunchCat(carrierController.Shuttle1);
                    }
                GUILayout.EndHorizontal();



            EditorGUILayout.LabelField("Catapult - 2", subHeadStyle);

                GUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Blast Shield");

                    if(GUILayout.Button("Raise Shield"))
                    {
                        carrierController.RaiseBackBlast(carrierController.BS2);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        carrierController.LowerBackBlast(carrierController.BS2);
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
                        carrierController.RaiseBackBlast(carrierController.BS3);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        carrierController.LowerBackBlast(carrierController.BS3);
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
                        carrierController.RaiseBackBlast(carrierController.BS4);
                    }

                    if(GUILayout.Button("Lower Shield"))
                    {
                        carrierController.LowerBackBlast(carrierController.BS4);
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
            carrierController.FA18E = (GameObject)EditorGUILayout.ObjectField("F18Prefab", carrierController.FA18E, typeof(GameObject), true);

            EditorGUILayout.Separator();
            if(GUILayout.Button("Spawn F18"))
            {
                carrierController.spawnFromHangar(0,0, carrierController.FA18E);
            }
        }
        else if(tab == 4)
        {
            
            GUILayout.Space(spacing);


            EditorGUILayout.LabelField("GameObjects", headStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


            EditorGUILayout.LabelField("Catapult - BackBlast Shields");

                carrierController.BS1 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 1", carrierController.BS1, typeof(GameObject), true);
                carrierController.BS2 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 2", carrierController.BS2, typeof(GameObject), true);
                carrierController.BS3 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 3", carrierController.BS3, typeof(GameObject), true);
                carrierController.BS4 = (GameObject)EditorGUILayout.ObjectField("Blast Shield 4", carrierController.BS4, typeof(GameObject), true);
            
            EditorGUILayout.LabelField("Catapult - Shuttles");

                carrierController.Shuttle1 = (GameObject)EditorGUILayout.ObjectField("Shuttle 1", carrierController.Shuttle1, typeof(GameObject), true);
                carrierController.Shuttle2 = (GameObject)EditorGUILayout.ObjectField("Shuttle 2", carrierController.Shuttle2, typeof(GameObject), true);
                carrierController.Shuttle3 = (GameObject)EditorGUILayout.ObjectField("Shuttle 3", carrierController.Shuttle3, typeof(GameObject), true);
                carrierController.Shuttle4 = (GameObject)EditorGUILayout.ObjectField("Shuttle 4", carrierController.Shuttle4, typeof(GameObject), true);  

            EditorGUILayout.LabelField("Elevators");

                carrierController.Elevator1 = (Transform)EditorGUILayout.ObjectField("Elevator 1", carrierController.Elevator1, typeof(Transform), true);
                carrierController.Elevator2 = (Transform)EditorGUILayout.ObjectField("Elevator 2", carrierController.Elevator2, typeof(Transform), true);
                carrierController.Elevator3 = (Transform)EditorGUILayout.ObjectField("Elevator 3", carrierController.Elevator3, typeof(Transform), true);


        }

       


        //base.OnInspectorGUI();


    }
}





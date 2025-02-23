using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Media;
using UnityEngine;

namespace GNB.Demo
{
    public class GunTester : MonoBehaviour
    {

        public enum WeaponType
        {
            Gun,
            AGM,
            Hydra
        }

        [Header("Current Weapon")]
        public WeaponType type = WeaponType.Gun;

        [Header("Weapon Options")]
        public Gun guns;
        public AAHardpoint agm;
        public AAHardpoint hydras;
        public Transform target;
        public float cooldownTime = .3f;

        [Header("HudOptions")]
        public GameObject AGMHud;
        public GameObject boreSight;
        public GameObject hydra;

        public List<GameObject> vehicleList = new List<GameObject>();
        public GameObject selectedTarget = null;
        public int targIndex = 0;


        private bool canShoot = true;
        private int i = 0;

        private void Start()
        {
            foreach (GameObject gv in GameObject.FindGameObjectsWithTag("GroundVehicle"))
            {
                vehicleList.Add(gv);
            }
        }

        private void Update()
        {
            switch (type)
            {
                case WeaponType.Gun:

                    boreSight.SetActive(true);
                    hydra.SetActive(false);
                    AGMHud.SetActive(false);


                    if (Input.GetButton("rightShoulder"))
                    {
                        guns.Fire(Vector3.zero);
                    }
                    break;

                case WeaponType.AGM:

                    boreSight.SetActive(false);
                    hydra.SetActive(false);
                    AGMHud.SetActive(true);

                    handleAGM();
                    if (Input.GetButtonDown("rightShoulder"))
                    {
                        agm.Launch(target);
                    }
                    break;

                case WeaponType.Hydra:

                    boreSight.SetActive(false);
                    hydra.SetActive(true);
                    AGMHud.SetActive(false);

                    if (Input.GetButton("rightShoulder"))
                    {
                        if (canShoot)
                        {
                            StartCoroutine(hydraFire());
                        }
                    }
                    break;
            }

            if (Input.GetButtonDown("leftShoulder"))
            {
                switch (type)
                {
                    case WeaponType.Gun:
                        type = WeaponType.AGM;
                        break;

                    case WeaponType.AGM:
                        type = WeaponType.Hydra;
                        break;

                    case WeaponType.Hydra:
                        type = WeaponType.Gun;
                        break;
                }
            }
        }

        protected void handleAGM()
        {
            if (Input.GetButtonDown("x_XButton"))
            {
                if (targIndex < vehicleList.Count-1)
                {
                    targIndex = targIndex + 1;
                }
                else
                {
                    targIndex = 0;
                }
                selectedTarget = vehicleList[targIndex];
                target = selectedTarget.transform;


                Debug.Log(vehicleList[targIndex]);
            }

        }

        public IEnumerator hydraFire()
        {
            //Instantiate your projectile
            hydras.Launch(target);
            canShoot = false;
            //wait for some time
            yield return new WaitForSeconds(cooldownTime);
            canShoot = true;
        }
    }
}

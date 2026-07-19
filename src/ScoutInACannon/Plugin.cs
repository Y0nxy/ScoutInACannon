using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

namespace ScoutInACannon
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        static ConfigEntry<KeyCode> emulateKey;
        static ConfigEntry<float> extraUp;
        static ConfigEntry<float> deleteTime;
        static bool holdingCannon = false;
        static Vector3 tubeForward;
        static Vector3 spawnPos;
        string ZombiePrefab = "MushroomZombie_Player";
        static GameObject ZombieObj;
        private void Awake()
        {
            Log = Logger;

            Log.LogInfo($"Plugin {Name} is loaded!");
            emulateKey = Config.Bind("hi dryeetman", "Emulate Key", KeyCode.X);
            extraUp = Config.Bind("hi dryeetman", "Up Correctment", 1.5f);
            deleteTime = Config.Bind("hi dryeetman", "deleteTime", 5f);
            Harmony harmony = new Harmony("ScoutInACannon");
            harmony.PatchAll();
        }

        void Update()
        {
            if (holdingCannon && Input.GetKeyDown(emulateKey.Value))
            {
                ZombieObj = Instantiate(Resources.Load<GameObject>(ZombiePrefab), spawnPos + Vector3.up * extraUp.Value, new Quaternion(0, 0, 0, 0));
                StartCoroutine(disableCollidersForASecond());
                ZombieObj.GetComponent<MushroomZombie>().isNPCZombie = false;
                LaunchTarget(ZombieObj.GetComponent<Character>());
                Destroy(ZombieObj, deleteTime.Value);
            }

        }
        private void LaunchTarget(Character c)
        {
            c.data.launchedByCannon = true;
            c.RPCA_Fall(1);
            c.AddForce(tubeForward * 2000f, 1f, 1f);// launchForce is 2000 for players
        }

        [HarmonyPatch(typeof(Constructable), "CreateOrMovePreview")]
        static class CannonGetter
        {
            [HarmonyPostfix]
            public static void Postfix(Constructable __instance)
            {
                var preview = __instance.currentPreview;
                if (preview != null && Input.GetKey(emulateKey.Value))
                {
                    Transform Cannon = preview.transform.GetChild(0).GetChild(0);
                    tubeForward = Cannon.forward;
                    spawnPos = Cannon.parent.GetChild(1).position;//spawn at cannon feet
                    log("set tubeforward and spawnpos");
                    return;
                }
                log("not found instance");
            }
        }
        [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.currentItem), MethodType.Setter)]
        public static class CurrentItemPatch
        {
            [HarmonyPostfix]
            static void Postfix(CharacterData __instance, Item value)
            {
                if (value == null) return;
                if (!__instance.character.IsLocal) return;

                if (value.UIData.itemName.Contains("Cannon")) holdingCannon = true;
                else holdingCannon = false;
                log("holdingCannon = "+holdingCannon);
            }
        }
        [HarmonyPatch(typeof(MushroomZombie))]
        class InstantZombie
        {
            [HarmonyPatch("HideAllRenderers")]
            [HarmonyPrefix]
            public static  bool stopHiding(MushroomZombie __instance)
            {
                //if (__instance.gameObject == ZombieObj)
                    return false;
                //return true;
            }
            [HarmonyPatch("FadeInRenderers")]
            static bool InstantRendereres(MushroomZombie __instance)
            {
                //if (__instance.gameObject != ZombieObj) return true;
                __instance.character.refs.customization.ShowAllRenderers();
                __instance.SetMushroomMan();
                for (int i = 0; i < __instance.character.refs.customization.refs.AllRenderers.Length; i++)
                {
                    for (int j = 0; j < __instance.character.refs.customization.refs.AllRenderers[i].materials.Length; j++)
                    {
                        __instance.character.refs.customization.refs.AllRenderers[i].materials[j].SetFloat("_Opacity", 1.5f);
                    }
                }

                return false;
            }
        }
        static void log(string msg)
        {
            Log.LogInfo(msg);
        }
        static System.Collections.IEnumerator disableCollidersForASecond()
        {
            if (ZombieObj == null) yield break;

            var collidersToRestore = new List<Collider>();

            foreach (var col in ZombieObj.GetComponentsInChildren<Collider>())
            {
                if (col != null && col.enabled)
                {
                    collidersToRestore.Add(col);
                    col.enabled = false; // Turn it off
                }
            }

             yield return new WaitForSeconds(0.25f);
            foreach (var col in collidersToRestore)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }
    }
}

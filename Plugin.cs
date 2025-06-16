using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Utilla.Attributes;
using TMPro;

namespace PizzaDelivery
{
    // If you're wondering why the code is so weird, it's because I've gotten used to doing luau in vStump, so I've essentially programmed this like I would do for one of those. Not exactly great
    [ModdedGamemode]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        ConfigFile config;
        ConfigEntry<int> highScoreSave;


        const float MIN_X = -35f;
        const float MIN_Z = -31f;

        const float MAX_X = -83f;
        const float MAX_Z = -81f; // Ignore the fact that the max is greater than the min

        const float MAX_HEIGHT = 28;

        GameObject ovenPrefab = null;
        GameObject ovenProps = null;
        GameObject clientHouse = null;
        GameObject pizza = null;
        GameObject pizzaPoint = null;

        TMP_Text text = null;
        TMP_Text highScore = null;
        TMP_Text timeLeftT = null;

        GameObject pizzaSpawnpoint = null;
        GameObject money = null;

        AudioSource music = null;
        AudioSource pickUp = null;
        AudioSource moneyCollect = null;

        int deliveriesMade = 0;

        int holdState = 0; // 0 = holding nothing, 1 = holding pizza in left hand, 2 = holding pizza in right hand, 3 = holding money in left hand, 4 = holding money in right hand                    disgusting

        bool pizzaTime = false;
        bool inRound = false;

        bool inRoom;

        void Awake()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "pizza_delivery.cfg");
            config = new ConfigFile(configPath, true);

            highScoreSave = config.Bind("save_data", "high_score", 0);
        }

        void LateUpdate()
        {
            if (pizzaTime)
            {
                if (inRound)
                {
                    int timeLeft = Mathf.RoundToInt(music.clip.length - music.time);
                    timeLeftT.text = timeLeft.ToString();

                    if (!music.isPlaying)
                    {
                        inRound = false;
                        holdState = 0;
                        pizza.transform.position = pizzaSpawnpoint.transform.position;
                        clientHouse.transform.position = new Vector3(0, -1000, 0);
                        GorillaTagger.Instance.StartVibration(false, 0.5f, 0.1f);
                        GorillaTagger.Instance.StartVibration(true, 0.5f, 0.1f);

                        if (deliveriesMade > highScoreSave.Value)
                        {
                            highScoreSave.Value = deliveriesMade;
                            highScore.text = highScoreSave.Value.ToString();
                            config.Save();
                        }
                    }
                }

                if (holdState == 0) // noble degrees of nesting
                {
                    if (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, pizzaPoint.transform.position) < 0.23f) // Magic numbers are the best type
                    {
                        holdState = 1;
                        DropNewClient();
                        pickUp.Play();

                        GorillaTagger.Instance.StartVibration(true, 0.5f, 0.1f);

                        if (!inRound)
                        {
                            deliveriesMade = 0;
                            inRound = true;
                            music.Play();
                        }
                    }
                    else if (Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, pizzaPoint.transform.position) < 0.23f)
                    {
                        holdState = 2;
                        DropNewClient();
                        pickUp.Play();

                        GorillaTagger.Instance.StartVibration(false, 0.5f, 0.1f);

                        if (!inRound)
                        {
                            deliveriesMade = 0;
                            inRound = true;
                            music.Play();
                        }
                    }
                }
                else if (holdState == 1)
                {
                    if (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, clientHouse.transform.position) < 0.5f)
                    {
                        holdState = 3;

                        GorillaTagger.Instance.StartVibration(true, 0.5f, 0.1f);
                        pickUp.Play();
                    }
                }
                else if (holdState == 2)
                {
                    if (Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, clientHouse.transform.position) < 0.5f)
                    {
                        holdState = 4;
                        GorillaTagger.Instance.StartVibration(false, 0.5f, 0.1f);
                        pickUp.Play();
                    }
                }
                else if (holdState == 3)
                {
                    if (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, ovenProps.transform.position) < 4f)
                    {
                        holdState = 0;
                        deliveriesMade++;
                        text.text = deliveriesMade.ToString();
                        GorillaTagger.Instance.StartVibration(true, 0.5f, 0.1f);
                        moneyCollect.Play();
                    }
                }
                else if (holdState == 4)
                {
                    if (Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, ovenProps.transform.position) < 4f)
                    {
                        holdState = 0;
                        deliveriesMade++;
                        text.text = deliveriesMade.ToString();
                        GorillaTagger.Instance.StartVibration(false, 0.5f, 0.1f);
                        moneyCollect.Play();
                    }
                }

                // Following lines of code are too beautiful for you to comprehend
                pizza.transform.position = pizzaSpawnpoint.transform.position;
                pizza.transform.rotation = pizzaSpawnpoint.transform.rotation;

                money.transform.position = new Vector3(0, -500, 0);
                if (holdState == 0) { pizza.transform.position = pizzaSpawnpoint.transform.position; pizza.transform.rotation = pizzaSpawnpoint.transform.rotation; }
                if (holdState == 1) { pizza.transform.position = GorillaTagger.Instance.leftHandTransform.position; pizza.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation; }
                if (holdState == 2) { pizza.transform.position = GorillaTagger.Instance.rightHandTransform.position; pizza.transform.rotation = GorillaTagger.Instance.rightHandTransform.rotation; }
                if (holdState == 3) { money.transform.position = GorillaTagger.Instance.leftHandTransform.position; money.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation; }
                if (holdState == 4) { money.transform.position = GorillaTagger.Instance.rightHandTransform.position; money.transform.rotation = GorillaTagger.Instance.rightHandTransform.rotation; }
            }
        }

        void DropNewClient()
        {
            Debug.Log("ok");
            while (true) // Nothing could go wrong here
            {
                RaycastHit rayHit;
                if (Physics.Raycast(new Vector3(UnityEngine.Random.Range(MIN_X, MAX_X), MAX_HEIGHT, UnityEngine.Random.Range(MIN_Z, MAX_Z)), Vector3.down, out rayHit, 40))
                {
                    if (Vector3.Dot(rayHit.normal, Vector3.up) > 0.5f && rayHit.point.y > -1.8f)
                    {
                        clientHouse.transform.position = rayHit.point;
                        return;
                    }
                }
            }
        }

        void CreateOven()
        {
            var ovenAssetBundle = LoadAssetBundle("PizzaDelivery.pizzaoven");

            if (ovenPrefab == null)
            {
                ovenPrefab = ovenAssetBundle.LoadAsset<GameObject>("PizzaOven");
            }

            ovenProps = Instantiate(ovenPrefab, new Vector3(-27.99f, 2.19f, -47.52f), Quaternion.identity); // Mysterious magic. Terrible magic
            ovenProps.AddComponent<GorillaSurfaceOverride>();

            clientHouse = GameObject.Find("_House");

            pizza = GameObject.Find("_Pizza");
            pizzaPoint = pizza.transform.GetChild(0).gameObject;

            pizzaSpawnpoint = GameObject.Find("_Pizza Spawn Point");
            money = GameObject.Find("_Dollar");

            text = GameObject.Find("_Score").GetComponent<TMP_Text>();
            text.text = "0";
            highScore = GameObject.Find("_High Score").GetComponent<TMP_Text>();
            highScore.text = highScoreSave.Value.ToString();

            timeLeftT = GameObject.Find("_Time Left").GetComponent<TMP_Text>();


            music = GameObject.Find("_Music").GetComponent<AudioSource>();
            pickUp = GameObject.Find("_Pizza Pick Up").GetComponent<AudioSource>();
            moneyCollect = GameObject.Find("_Ding").GetComponent<AudioSource>();
            pizzaTime = true;
        }
        void DestroyOven()
        {
            if (ovenProps != null)
            {
                Destroy(ovenProps.gameObject);
            }

            pizzaTime = false;
        }

        public AssetBundle LoadAssetBundle(string path)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            AssetBundle bundle = AssetBundle.LoadFromStream(stream);
            stream.Close();
            return bundle;
        }

        [ModdedGamemodeJoin]
        public void OnJoin(string gamemode)
        {
            CreateOven();
            inRoom = true;
        }

        [ModdedGamemodeLeave]
        public void OnLeave(string gamemode)
        {
            DestroyOven();
            inRoom = false;
        }
    }
}
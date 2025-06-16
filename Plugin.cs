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
        enum GameState
        {
            Nothing, 
            LeftPizza,
            RightPizza,
            LeftMoney,
            RightMoney
        }

        ConfigFile config;
        ConfigEntry<int> highScoreSave;


        Vector3 min = new Vector3(-83f, -1.8f, -81f);
        Vector3 max = new Vector3(-35f, 28f, -31f);

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

        GameState holdState = GameState.Nothing;

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
                        holdState = GameState.Nothing;
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

                if (holdState == GameState.Nothing) // noble degrees of nesting
                {
                    HoldingNothing();
                }
                if (holdState == GameState.LeftPizza || holdState == GameState.RightPizza)
                {
                    HoldingPizza(holdState == GameState.RightPizza);
                    pizza.transform.position = holdState == GameState.RightPizza ? GorillaTagger.Instance.rightHandTransform.position : GorillaTagger.Instance.leftHandTransform.position;
                    pizza.transform.rotation = holdState == GameState.RightPizza ? GorillaTagger.Instance.rightHandTransform.rotation :GorillaTagger.Instance.leftHandTransform.rotation;
                }
                if (holdState == GameState.LeftMoney || holdState == GameState.RightMoney)
                {
                    HoldingMoney(holdState == GameState.RightMoney);
                    money.transform.position = holdState == GameState.RightMoney ? GorillaTagger.Instance.rightHandTransform.position : GorillaTagger.Instance.leftHandTransform.position;
                    money.transform.rotation = holdState == GameState.RightMoney ? GorillaTagger.Instance.rightHandTransform.rotation : GorillaTagger.Instance.leftHandTransform.rotation;
                }
            }
        }

        void HoldingNothing()
        {
            if (Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, pizzaPoint.transform.position) < 0.23f) // Magic numbers are the best type
            {
                holdState = GameState.LeftPizza;
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
                holdState = GameState.RightPizza;
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

            pizza.transform.position = pizzaSpawnpoint.transform.position; 
            pizza.transform.rotation = pizzaSpawnpoint.transform.rotation;
            money.transform.position = new Vector3(0, -500, 0);
        }

        void HoldingPizza(bool hand) // false left, true right
        {
            if (Vector3.Distance(pizza.transform.position, clientHouse.transform.position) < 0.5f)
            {
                holdState = hand ? GameState.RightMoney : GameState.LeftMoney;

                GorillaTagger.Instance.StartVibration(!hand, 0.5f, 0.1f);
                pickUp.Play();
            }

            money.transform.position = new Vector3(0, -500, 0);
        }
        void HoldingMoney(bool hand) // false left, true right
        {
            Vector3 handPos = hand ? GorillaTagger.Instance.rightHandTransform.position : GorillaTagger.Instance.leftHandTransform.position;
            if (Vector3.Distance(handPos, ovenProps.transform.position) < 4f)
            {
                holdState = GameState.Nothing;
                deliveriesMade++;
                text.text = deliveriesMade.ToString();
                GorillaTagger.Instance.StartVibration(!hand, 0.5f, 0.1f);
                moneyCollect.Play();
            }
            pizza.transform.position = pizzaSpawnpoint.transform.position;
            pizza.transform.rotation = pizzaSpawnpoint.transform.rotation;
        }

        void DropNewClient()
        {
            Debug.Log("ok");
            for (int i = 0; i < 50000; i++) // Nothing could go wrong here
            {
                RaycastHit rayHit;
                if (Physics.Raycast(new Vector3(UnityEngine.Random.Range(min.x, max.x), max.y, UnityEngine.Random.Range(min.z, max.z)), Vector3.down, out rayHit, 40))
                {
                    if (Vector3.Dot(rayHit.normal, Vector3.up) > 0.5f && rayHit.point.y > min.y)
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
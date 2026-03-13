using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.SimpleLocalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace verho_tool;


public class Item
{
    public enum ItemType
    {
        Weapon,
        Rune,
        Consumable,
        Ring,
        Key,
        Material
    };

    public ItemType itemType;
    public int itemId;
    public string itemName;
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Verho.exe")]
public class VerhoTool : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private GUISkin guiSkin = new GUISkin();

    private Rect windowRectMainDefault = new Rect(50, 50, 480, 200);
    private Rect windowRectItemSpawnerDefault = new Rect(50, 50, 300, 400);
    private Rect windowRectBonfiresDefault = new Rect(50, 50, 200, 400);
    private Rect windowRectMaprenderDefault = new Rect(50, 50, 210, 155);

    private Rect windowRectMain = new Rect(0, 0, 0, 0);
    private Rect windowRectItemSpawner = new Rect(0, 0, 0, 0);
    private Rect windowRectBonfires = new Rect(0, 0, 0, 0);
    private Rect windowRectMaprender = new Rect(0, 0, 0, 0);

    private bool windowEnabledMain = false;
    private bool windowEnabledItemSpawner = false;
    private bool windowEnabledBonfires = false;
    private bool windowEnabledMaprender = false;


    private Vector3[] storedPositions = { Vector3.zero, Vector3.zero, Vector3.zero};
    private float storedGravity = 10f;

    private Vector3 teleportDownVector = new Vector3(0.0f, -1.0f, 0.0f);
    private Vector3 teleportUpVector = new Vector3(0.0f, 1.0f, 0.0f);

    private bool freecamEnabled = false;
    private GameObject freecamStoredPlayerObject = null;
    private GameObject freecamCameraObject = null;
    private Vector3 freecamPlayerCameraOffset = Vector3.zero;
    private Animator freecamPlayerBlackscreenAnimator = null;

    private List<Item> itemList = new List<Item>();
    private bool itemListInitialized = false;
    private Vector2 itemListPosition;
    private Item selectedItem;
    private int itemCount = 1;
    
    private Vector2 bonfireListPosition;

    private Vector3 maprenderPosition = Vector3.zero;
    private float maprenderSize = 100.0f;
    private int maprenderResolution = 1024;


    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Set default window rects
        windowRectMain = windowRectMainDefault;
        windowRectItemSpawner = windowRectItemSpawnerDefault;
        windowRectBonfires = windowRectBonfiresDefault;
        windowRectMaprender = windowRectMaprenderDefault;
    }

    private void Update()
    {
        // Dumb workaround for blackscreen after exiting freecam
        if (freecamPlayerBlackscreenAnimator != null)
        {
            freecamPlayerBlackscreenAnimator.speed = 1.0f;
            freecamPlayerBlackscreenAnimator = null;
        }

        // Toggle Tool GUI
        if (Input.GetKeyDown(KeyCode.F11))
        {
            windowEnabledMain = !windowEnabledMain;
        }

        // Save Positions
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SavePosition(0);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                SavePosition(1);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                SavePosition(2);
            }
        }
        else
        {
            // Load Positions
            if (Input.GetKeyDown(KeyCode.F1))
            {
                LoadPosition(0);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                LoadPosition(1);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                LoadPosition(2);
            }
        }

        // No Gravity
        if (Input.GetKeyDown(KeyCode.F4))
        {
            GameObject player = GetPlayer();
            if (player != null)
            {
                FpsController fpsController = player.GetComponent<FpsController>();
                if (fpsController != null)
                {
                    if (fpsController.gravity == 0)
                    {
                        fpsController.gravity = storedGravity;
                    }
                    else
                    {
                        storedGravity = fpsController.gravity;
                        fpsController.gravity = 0;

                        fpsController.fd_startPointY = fpsController.fd_curY; // Cancel fall damage
                        
                        fpsController.move = Vector3.zero;
                        Traverse.Create(fpsController).Field("moveDirection").SetValue(Vector3.zero);
                    }
                }
            }
        }

        // Move position up/down
        if (Input.GetKey(KeyCode.PageUp))
        {
            TeleportPlayer(teleportUpVector * Time.deltaTime * 5.0f, true);
        }
        if (Input.GetKey(KeyCode.PageDown))
        {
            TeleportPlayer(teleportDownVector * Time.deltaTime * 5.0f, true);
        }

        // Toggle freecam
        if (Input.GetKeyDown(KeyCode.F7))
        {
            ToggleFreecam(false);
        }
        else if (Input.GetKeyDown(KeyCode.F8) && freecamEnabled == true)
        {
            ToggleFreecam(true);
        }

        // Handle freecam
        if (freecamEnabled)
        {
            float moveSpeed = 5.0f;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                moveSpeed = 25.0f;
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                moveSpeed = 1.0f;
            }

            float turnSpeed = 1.0f;
            float h = Input.GetAxis("Mouse X") * turnSpeed;
            float v = -Input.GetAxis("Mouse Y") * turnSpeed;
            freecamCameraObject.transform.eulerAngles += new Vector3(v, h, 0.0f);

            if (Input.GetKey(KeyCode.W))
            {
                freecamCameraObject.transform.position += freecamCameraObject.transform.forward * Time.deltaTime * moveSpeed;
            }
            if (Input.GetKey(KeyCode.S))
            {
                freecamCameraObject.transform.position += -freecamCameraObject.transform.forward * Time.deltaTime * moveSpeed;
            }
            if (Input.GetKey(KeyCode.A))
            {
                freecamCameraObject.transform.position += -freecamCameraObject.transform.right * Time.deltaTime * moveSpeed;
            }
            if (Input.GetKey(KeyCode.D))
            {
                freecamCameraObject.transform.position += freecamCameraObject.transform.right * Time.deltaTime * moveSpeed;
            }
        }
    }

    private void OnGUI()
	{
        // Set up GUI skin
        guiSkin = GUI.skin;

        guiSkin.label.margin = new RectOffset(5, 5, 0, 0);
        guiSkin.label.padding = new RectOffset(5, 5, 0, 0);
        guiSkin.label.border = new RectOffset(5, 5, 0, 0);

        guiSkin.button.margin = new RectOffset(5, 5, 0, 0);
        guiSkin.button.padding = new RectOffset(5, 5, 0, 0);

        // Render windows
        if (windowEnabledMain)
        {
            windowRectMain = GUI.Window(10000, windowRectMain, WindowMain, "Verho Tool [F11]");

            if (windowEnabledItemSpawner)
            {
                windowRectItemSpawner = GUI.Window(10001, windowRectItemSpawner, WindowItemSpawner, "Item Spawner");
            }

            if (windowEnabledBonfires)
            {
                windowRectBonfires = GUI.Window(10002, windowRectBonfires, WindowBonfires, "Mask Altars");
            }

            if (windowEnabledMaprender)
            {
                windowRectMaprender = GUI.Window(10003, windowRectMaprender, WindowMaprender, "Map Renderer");
            }
        }
	}

    private void WindowMain(int windowID)
    {
        // Styling
        GUI.skin = guiSkin;


        // Get some components
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }

        FpsController fpsController = player.GetComponent<FpsController>();
        if (fpsController == null)
        {
            return;
        }

        PlayerHealthController healthController = player.GetComponent<PlayerHealthController>();
        if (healthController == null)
        {
            return;
        }

        Progress progress = Traverse.Create(fpsController).Field("progress").GetValue() as Progress;
        if (progress == null)
        {
            return;
        }
        


        // Start GUI
        GUILayout.BeginVertical();

        
        // Gravity + Position
        GUILayout.BeginHorizontal();
        if (fpsController.gravity == 0)
        {
            if (GUILayout.Button("[F4] Grav: Off", GUILayout.Width(100)))
            {
                fpsController.gravity = storedGravity;
            }
        }
        else
        {
            if (GUILayout.Button("[F4] Grav: On", GUILayout.Width(100)))
            {
                storedGravity = fpsController.gravity;
                fpsController.gravity = 0;

                fpsController.fd_startPointY = fpsController.fd_curY; // Cancel fall damage
                
                fpsController.move = Vector3.zero;
                Traverse.Create(fpsController).Field("moveDirection").SetValue(Vector3.zero);
            }
        }
        
        if (GUILayout.Button("Up", GUILayout.Width(30)))
        {
            TeleportPlayer(teleportUpVector, true);
        }
        if (GUILayout.Button("Down", GUILayout.Width(45)))
        {
            TeleportPlayer(teleportDownVector, true);
        }

        Vector3 playerPosition = player.transform.position;
        GUILayout.Label(string.Format("XYZ: {0}, {1}, {2}", playerPosition.x, playerPosition.y, playerPosition.z));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Stored positions
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[Shift + F1] Save", GUILayout.Width(120)))
        {
            SavePosition(0);
        }
        if (GUILayout.Button("[F1] Load", GUILayout.Width(75)))
        {
            LoadPosition(0);
        }
        GUILayout.Label(string.Format("#1: {0}, {1}, {2}", storedPositions[0].x, storedPositions[0].y, storedPositions[0].z));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[Shift + F2] Save", GUILayout.Width(120)))
        {
            SavePosition(1);
        }
        if (GUILayout.Button("[F2] Load", GUILayout.Width(75)))
        {
            LoadPosition(1);
        }
        GUILayout.Label(string.Format("#2: {0}, {1}, {2}", storedPositions[1].x, storedPositions[1].y, storedPositions[1].z));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[Shift + F3] Save", GUILayout.Width(120)))
        {
            SavePosition(2);
        }
        if (GUILayout.Button("[F3] Load", GUILayout.Width(75)))
        {
            LoadPosition(2);
        }
        GUILayout.Label(string.Format("#3: {0}, {1}, {2}", storedPositions[2].x, storedPositions[2].y, storedPositions[2].z));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Freecam
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[F7] Toggle Freecam"))
        {
            ToggleFreecam(false);
        }
        if (GUILayout.Button("[F8] Teleport to Freecam") && freecamEnabled == true)
        {
            ToggleFreecam(true);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // HP + Mana
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal Full", GUILayout.Width(80)))
        {
            progress.health = progress.maxHealth;
        }
        if (GUILayout.Button("Mana Full", GUILayout.Width(80)))
        {
            progress.mana = progress.maxMana;
        }
        GUILayout.Label(string.Format("Health: {0}/{1}, Mana: {2}/{3}", progress.health, progress.maxHealth, progress.mana, progress.maxMana));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // EXP
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10000", GUILayout.Width(60)))
        {
            progress.currentExp -= 10000;
        }
        if (GUILayout.Button("-100", GUILayout.Width(45)))
        {
            progress.currentExp -= 100;
        }
        if (GUILayout.Button("-1", GUILayout.Width(30)))
        {
            progress.currentExp -= 1;
        }
        GUILayout.Label(string.Format("EXP: {0}", progress.currentExp));
        if (GUILayout.Button("+1", GUILayout.Width(30)))
        {
            progress.currentExp += 1;
        }
        if (GUILayout.Button("+100", GUILayout.Width(45)))
        {
            progress.currentExp += 100;
        }
        if (GUILayout.Button("+10000", GUILayout.Width(60)))
        {
            progress.currentExp += 10000;
        }
        GUILayout.EndHorizontal();

        // Gold
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10000", GUILayout.Width(60)))
        {
            progress.gold -= 10000;
        }
        if (GUILayout.Button("-100", GUILayout.Width(45)))
        {
            progress.gold -= 100;
        }
        if (GUILayout.Button("-1", GUILayout.Width(30)))
        {
            progress.gold -= 1;
        }
        GUILayout.Label(string.Format("Gold: {0}", progress.gold));
        if (GUILayout.Button("+1", GUILayout.Width(30)))
        {
            progress.gold += 1;
        }
        if (GUILayout.Button("+100", GUILayout.Width(45)))
        {
            progress.gold += 100;
        }
        if (GUILayout.Button("+10000", GUILayout.Width(60)))
        {
            progress.gold += 10000;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);


        // Item Spawner + Bonfires
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Item Spawner"))
        {
            windowEnabledItemSpawner = !windowEnabledItemSpawner;
            windowRectItemSpawner = windowRectItemSpawnerDefault;
        }
        if (GUILayout.Button("Mask Altars"))
        {
            windowEnabledBonfires = !windowEnabledBonfires;
            windowRectBonfires = windowRectBonfiresDefault;
        }
        if (GUILayout.Button("Map Renderer"))
        {
            windowEnabledMaprender = !windowEnabledMaprender;
            windowRectMaprender = windowRectMaprenderDefault;
        }
        GUILayout.EndHorizontal();


        // End
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void WindowItemSpawner(int windowID)
    {
        // Styling
        GUI.skin = guiSkin;


        // Get some components
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }

        FpsController fpsController = player.GetComponent<FpsController>();
        if (fpsController == null)
        {
            return;
        }

        Progress progress = Traverse.Create(fpsController).Field("progress").GetValue() as Progress;
        if (progress == null)
        {
            return;
        }

        InventorySaveMenager inventorySave = progress.GetComponent<InventorySaveMenager>();
        if (inventorySave == null)
        {
            return;
        }
        

        // Start GUI
        GUILayout.BeginVertical();


        // Spawn Item + Close buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Item") && selectedItem != null)
        {
            if (selectedItem.itemType == Item.ItemType.Weapon)
            {
                inventorySave.inv_weapons[selectedItem.itemId] = -1;
            }

            GameObject itemSpawnObject = new GameObject();
            itemSpawnObject.SetActive(false);

            ItemGiver itemGiver = itemSpawnObject.AddComponent<ItemGiver>();
            itemGiver.itemType = (int)selectedItem.itemType;
            itemGiver.itemId = selectedItem.itemId;
            itemGiver.howMany = itemCount;

            itemSpawnObject.SetActive(true);
        }

        if (GUILayout.Button("Close"))
        {
            windowEnabledItemSpawner = false;
        }
        GUILayout.EndHorizontal();

        // Amount/Level control
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-"))
        {
            itemCount--;

            if (itemCount < 1)
            {
                itemCount = 1;
            }
        }
        GUILayout.Label($"Amount/Level: {itemCount}");
        if (GUILayout.Button("+"))
        {
            itemCount++;

            if (itemCount > 99)
            {
                itemCount = 99;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Get item list if uninitialized
        if (!itemListInitialized)
        {
            // Get all items
            for (int i = 0; i < 256; i++)
            {
                if (LocalizationManager.HasKey("WepName." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Weapon;
                    item.itemName = LocalizationManager.Localize("WepName." + i.ToString());

                    itemList.Add(item);
                }

                if (LocalizationManager.HasKey("RuneName." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Rune;
                    item.itemName = LocalizationManager.Localize("RuneName." + i.ToString());
                    
                    itemList.Add(item);
                }

                if (LocalizationManager.HasKey("UsableName." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Consumable;
                    item.itemName = LocalizationManager.Localize("UsableName." + i.ToString());
                    
                    itemList.Add(item);
                }

                if (LocalizationManager.HasKey("RingName." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Ring;
                    item.itemName = LocalizationManager.Localize("RingName." + i.ToString());
                    
                    itemList.Add(item);
                }

                if (LocalizationManager.HasKey("Key." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Key;
                    item.itemName = LocalizationManager.Localize("Key." + i.ToString());
                    
                    itemList.Add(item);
                }

                if (LocalizationManager.HasKey("Mat." + i.ToString()))
                {
                    Item item = new Item();
                    item.itemId = i;
                    item.itemType = Item.ItemType.Material;
                    item.itemName = LocalizationManager.Localize("Mat." + i.ToString());
                    
                    itemList.Add(item);
                }
            }
            
            itemList = itemList.OrderBy(x => x.itemType).ToList();
            itemListInitialized = true;
        }

        // Item list
        itemListPosition = GUILayout.BeginScrollView(itemListPosition/*, GUILayout.Width(300), GUILayout.Height(200)*/);
        foreach (Item item in itemList)
        {
            if (item == selectedItem)
            {
                if (GUILayout.Button($">> [{item.itemType}, {item.itemId}] {item.itemName} <<"))
                {
                    selectedItem = item;
                }
            }
            else
            {
                if (GUILayout.Button($"[{item.itemType}, {item.itemId}] {item.itemName}"))
                {
                    selectedItem = item;
                }
            }
        }
        GUILayout.EndScrollView();
        /*
        if (GUILayout.Button("Print Items"))
        {
            // Print all items
            foreach (Item item in itemList)
            {
                Logger.LogInfo($"Type: {item.itemType}, ID: {item.itemId}, Name: {item.itemName}");
            }
        }
        */


        // End
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void WindowBonfires(int windowID)
    {
        // Styling
        GUI.skin = guiSkin;


        // Get some components
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }

        FpsController fpsController = player.GetComponent<FpsController>();
        if (fpsController == null)
        {
            return;
        }

        Progress progress = Traverse.Create(fpsController).Field("progress").GetValue() as Progress;
        if (progress == null)
        {
            return;
        }

        WorldSaveMenager worldsave = Traverse.Create(fpsController).Field("worldSave").GetValue() as WorldSaveMenager;
        if (worldsave == null)
        {
            return;
        }
        

        // Start GUI
        GUILayout.BeginVertical();


        // Close button
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Close"))
        {
            windowEnabledBonfires = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);


        // Bonfire list
        bonfireListPosition = GUILayout.BeginScrollView(bonfireListPosition);
        int bonfireCount = worldsave.world_bonfireLocation.Count;
        for (int i = 0; i < bonfireCount; i++)
        {
            GUILayout.BeginHorizontal();

            if (worldsave.world_bonfireUnloacked[i])
            {
                if (GUILayout.Button("Unlocked", GUILayout.Width(80)))
                {
                    worldsave.world_bonfireUnloacked[i] = false;
                }
            }
            else
            {
                if (GUILayout.Button("Locked", GUILayout.Width(80)))
                {
                    worldsave.world_bonfireUnloacked[i] = true;
                }
            }

            GUILayout.Label("ID: " + i.ToString());

            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();


        // End
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void WindowMaprender(int windowID)
    {
        // Styling
        GUI.skin = guiSkin;


        // Get some components
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }
        

        // Start GUI
        GUILayout.BeginVertical();


        // Render + Close buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Render"))
        {
            TriggerMaprender();
        }

        if (GUILayout.Button("Close"))
        {
            windowEnabledMaprender = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);


        // Render position
        GUILayout.Label("Render XYZ:");

        String stringMaprenderPosX = GUI.TextField(new Rect(10, 60, 60, 20), maprenderPosition.x.ToString());
        String stringMaprenderPosY = GUI.TextField(new Rect(75, 60, 60, 20), maprenderPosition.y.ToString());
        String stringMaprenderPosZ = GUI.TextField(new Rect(140, 60, 60, 20), maprenderPosition.z.ToString());

        float outMaprenderPosX;
        if (float.TryParse(stringMaprenderPosX, out outMaprenderPosX))
        {
            maprenderPosition.x = outMaprenderPosX;
        }

        float outMaprenderPosY;
        if (float.TryParse(stringMaprenderPosY, out outMaprenderPosY))
        {
            maprenderPosition.y = outMaprenderPosY;
        }

        float outMaprenderPosZ;
        if (float.TryParse(stringMaprenderPosZ, out outMaprenderPosZ))
        {
            maprenderPosition.z = outMaprenderPosZ;
        }

        GUILayout.Space(25);

        if (GUILayout.Button("Current Camera Position"))
        {
            maprenderPosition = Camera.main.transform.position;
        }

        GUILayout.Space(10);


        // Render size + resolution
        GUILayout.Label("Render Size & Resolution:");

        String stringMaprenderSize = GUI.TextField(new Rect(10, 125, 60, 20), maprenderSize.ToString());
        String stringMaprenderResolution = GUI.TextField(new Rect(75, 125, 60, 20), maprenderResolution.ToString());

        float outMaprenderSize;
        if (float.TryParse(stringMaprenderSize, out outMaprenderSize))
        {
            maprenderSize = outMaprenderSize;
        }

        int outMaprenderResolution;
        if (int.TryParse(stringMaprenderResolution, out outMaprenderResolution))
        {
            maprenderResolution = outMaprenderResolution;
        }


        // End
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private GameObject GetPlayer()
    {
        // GameObject player = GameObject.Find("Player");

        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        if (playerObjects.Length < 1)
        {
            return null;
        }
        GameObject player = playerObjects[0];

        return player;
    }

    private void SavePosition(int index)
    {
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }

        if (index <= storedPositions.Length - 1)
        {
            storedPositions[index] = player.transform.position;
        }
    }

    private void LoadPosition(int index)
    {
        if (index <= storedPositions.Length - 1)
        {
            TeleportPlayer(storedPositions[index], false);
        }
    }

    private void TeleportPlayer(Vector3 position, bool relative)
    {
        GameObject player = GetPlayer();
        if (player == null)
        {
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            bool controllerEnabled = controller.enabled;
            controller.enabled = false;

            if (relative == true)
            {
                player.transform.position += position;
            }
            else
            {
                player.transform.position = position;
            }
            

            controller.enabled = controllerEnabled;
        }
    }

    private void ToggleFreecam(bool teleport)
    {
        if (freecamEnabled == false)
        {
            GameObject player = GetPlayer();
            if (player == null)
            {
                return;
            }

            // Create freecam camera by copying the current main camera
            GameObject mainCameraObject = Camera.main.gameObject;
            freecamCameraObject = Instantiate(mainCameraObject);
            freecamCameraObject.tag = "MainCamera";
            freecamCameraObject.transform.position = mainCameraObject.transform.position;
            freecamCameraObject.transform.eulerAngles = mainCameraObject.transform.eulerAngles;

            // Get rid of all child objects like the compass and arms
            while (freecamCameraObject.transform.childCount > 0)
            {
                DestroyImmediate(freecamCameraObject.transform.GetChild(0).gameObject);
            }

            // Store offset between player and camera position
            freecamPlayerCameraOffset = player.transform.position - mainCameraObject.transform.position;

            // Store current player for later and disable it
            freecamStoredPlayerObject = player;
            player.SetActive(false);

            freecamEnabled = true;
        }
        else
        {
            if (teleport)
            {
                freecamStoredPlayerObject.transform.position = freecamCameraObject.transform.position + freecamPlayerCameraOffset;
                
                Vector3 newEulerAngles = freecamStoredPlayerObject.transform.eulerAngles;
                newEulerAngles.y = freecamCameraObject.transform.eulerAngles.y;
                freecamStoredPlayerObject.transform.eulerAngles = newEulerAngles;
            }

            Destroy(freecamCameraObject);
            freecamStoredPlayerObject.SetActive(true);

            freecamEnabled = false;

            // This is dumb
            Animator blackscreenAnimator = GameObject.FindFirstObjectByType<LoadGravityFixer>().gameObject.GetComponent<Animator>();
            if (blackscreenAnimator != null)
            {
                blackscreenAnimator.speed = 9999999999.0f;
                freecamPlayerBlackscreenAnimator = blackscreenAnimator;
            }
        }
    }

    private void TriggerMaprender()
    {
        GameObject camObject = new GameObject();
        camObject.transform.position = maprenderPosition;
        camObject.transform.eulerAngles = new Vector3(90.0f, 0.0f, 0.0f);


        Camera cam = camObject.AddComponent<Camera>();
        cam.enabled = false;

        cam.orthographic = true;
        cam.orthographicSize = maprenderSize;
        cam.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);

        cam.useOcclusionCulling = false;
        cam.nearClipPlane = 0.0f;
        cam.farClipPlane = 1000000.0f;


        float oldBias = QualitySettings.lodBias;
        QualitySettings.lodBias = 1000000.0f;

        bool oldFog = RenderSettings.fog;
        RenderSettings.fog = false;


        RenderTexture rt = new RenderTexture(maprenderResolution, maprenderResolution, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(maprenderResolution, maprenderResolution, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, maprenderResolution, maprenderResolution), 0, 0);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Destroy(rt);


        byte[] bytes;
        bytes = tex.EncodeToPNG();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"verho-maprender-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
        System.IO.File.WriteAllBytes(path, bytes);


        QualitySettings.lodBias = oldBias;
        RenderSettings.fog = oldFog;

        Destroy(camObject);
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LADIDMainMenu : MonoBehaviour
{
    [Serializable]
    public sealed class MenuConfig
    {
        public string title="LADID";
        public string subtitle="LIFE AND DEATH IN DIMENSION";
        public string startLabel="게임 시작";
        public string settingsLabel="설정";
        public string quitLabel="게임 나가기";
        public string settingsTitle="설정";
        public string volumeLabel="마스터 볼륨";
        public string fullscreenLabel="전체 화면";
        public string backLabel="뒤로";
        public string accentColor="#55D6FF";
        public int titleFontSize=78;
        public int subtitleFontSize=18;
        public int buttonFontSize=20;
        public string titleFontFamily="Arial";
        public string subtitleFontFamily="Arial";
        public string buttonFontFamily="Malgun Gothic";
        public TextBoxConfig[] textBoxes=new TextBoxConfig[0];
    }

    [Serializable]
    public sealed class TextBoxConfig
    {
        public string text="새 텍스트";
        public float x=50f;
        public float y=50f;
        public float width=36f;
        public float height=10f;
        public int fontSize=24;
        public string fontFamily="Malgun Gothic";
        public string color="#FFFFFF";
        public string alignment="Center";
    }

    static readonly Dictionary<string,Font> FontCache=new Dictionary<string,Font>();
    MenuConfig config;
    Color accent;
    GameObject settingsOverlay;
    Slider volumeSlider;
    Text volumeValue;
    Toggle fullscreenToggle;

    void Start()
    {
        Application.targetFrameRate=60;
        config=LoadConfig();
        if(!ColorUtility.TryParseHtmlString(config.accentColor,out accent))accent=new Color(.33f,.84f,1f);
        EnsureEventSystem();
        BuildMenu();
    }

    MenuConfig LoadConfig()
    {
        var defaults=new MenuConfig();string path=Path.Combine(Application.streamingAssetsPath,"main-menu.json");
        if(!File.Exists(path))return defaults;
        try
        {
            MenuConfig loaded=JsonUtility.FromJson<MenuConfig>(File.ReadAllText(path));
            if(loaded==null)return defaults;
            if(string.IsNullOrWhiteSpace(loaded.title))loaded.title=defaults.title;
            if(string.IsNullOrWhiteSpace(loaded.subtitle))loaded.subtitle=defaults.subtitle;
            if(string.IsNullOrWhiteSpace(loaded.startLabel))loaded.startLabel=defaults.startLabel;
            if(string.IsNullOrWhiteSpace(loaded.settingsLabel))loaded.settingsLabel=defaults.settingsLabel;
            if(string.IsNullOrWhiteSpace(loaded.quitLabel))loaded.quitLabel=defaults.quitLabel;
            if(string.IsNullOrWhiteSpace(loaded.settingsTitle))loaded.settingsTitle=defaults.settingsTitle;
            if(string.IsNullOrWhiteSpace(loaded.volumeLabel))loaded.volumeLabel=defaults.volumeLabel;
            if(string.IsNullOrWhiteSpace(loaded.fullscreenLabel))loaded.fullscreenLabel=defaults.fullscreenLabel;
            if(string.IsNullOrWhiteSpace(loaded.backLabel))loaded.backLabel=defaults.backLabel;
            if(string.IsNullOrWhiteSpace(loaded.accentColor))loaded.accentColor=defaults.accentColor;
            if(loaded.titleFontSize<=0)loaded.titleFontSize=defaults.titleFontSize;
            if(loaded.subtitleFontSize<=0)loaded.subtitleFontSize=defaults.subtitleFontSize;
            if(loaded.buttonFontSize<=0)loaded.buttonFontSize=defaults.buttonFontSize;
            if(string.IsNullOrWhiteSpace(loaded.titleFontFamily))loaded.titleFontFamily=defaults.titleFontFamily;
            if(string.IsNullOrWhiteSpace(loaded.subtitleFontFamily))loaded.subtitleFontFamily=defaults.subtitleFontFamily;
            if(string.IsNullOrWhiteSpace(loaded.buttonFontFamily))loaded.buttonFontFamily=defaults.buttonFontFamily;
            if(loaded.textBoxes==null)loaded.textBoxes=defaults.textBoxes;
            return loaded;
        }
        catch(Exception exception)
        {
            Debug.LogWarning("Could not read main-menu.json. Using built-in menu defaults. "+exception.Message);
            return defaults;
        }
    }

    void EnsureEventSystem()
    {
        if(FindObjectOfType<EventSystem>())return;
        var events=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        DontDestroyOnLoad(events);
    }

    void BuildMenu()
    {
        var canvasObject=new GameObject("Main Menu Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        Canvas canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
        CanvasScaler scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        Color backgroundColor;if(!ColorUtility.TryParseHtmlString("#101923",out backgroundColor))backgroundColor=new Color(.06f,.09f,.14f);
        MakeImage(canvas.transform,"Background",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,backgroundColor);
        MakeImage(canvas.transform,"Top accent",new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0f,-5f),new Vector2(300f,3f),accent);
        MakeText(canvas.transform,config.title,config.titleFontSize,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0f,153f),new Vector2(760f,100f),Color.white,config.titleFontFamily);
        MakeText(canvas.transform,config.subtitle.ToUpperInvariant(),config.subtitleFontSize,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0f,103f),new Vector2(760f,34f),new Color(.67f,.76f,.84f),config.subtitleFontFamily);
        MakeImage(canvas.transform,"Title underline",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0f,77f),new Vector2(76f,2f),new Color(accent.r,accent.g,accent.b,.75f));

        MakeButton(canvas.transform,config.startLabel,new Vector2(.5f,.5f),new Vector2(0f,14f),new Vector2(350f,58f),accent,config.buttonFontSize,config.buttonFontFamily,StartGame);
        MakeButton(canvas.transform,config.settingsLabel,new Vector2(.5f,.5f),new Vector2(0f,-62f),new Vector2(350f,58f),new Color(.15f,.22f,.29f),config.buttonFontSize,config.buttonFontFamily,OpenSettings);
        MakeButton(canvas.transform,config.quitLabel,new Vector2(.5f,.5f),new Vector2(0f,-138f),new Vector2(350f,58f),new Color(.15f,.22f,.29f),config.buttonFontSize,config.buttonFontFamily,QuitGame);
        MakeText(canvas.transform,"A SURVIVAL JOURNEY THROUGH COLLAPSING DIMENSIONS",12,TextAnchor.MiddleCenter,new Vector2(.5f,0f),new Vector2(0f,24f),new Vector2(900f,24f),new Color(.43f,.54f,.62f));
        BuildCustomTextBoxes(canvas.transform);
        BuildSettingsPanel(canvas.transform);
    }

    void BuildSettingsPanel(Transform canvas)
    {
        settingsOverlay=new GameObject("Settings Overlay",typeof(RectTransform),typeof(Image));settingsOverlay.transform.SetParent(canvas,false);
        RectTransform overlayRect=settingsOverlay.GetComponent<RectTransform>();overlayRect.anchorMin=Vector2.zero;overlayRect.anchorMax=Vector2.one;overlayRect.offsetMin=Vector2.zero;overlayRect.offsetMax=Vector2.zero;
        settingsOverlay.GetComponent<Image>().color=new Color(.015f,.025f,.04f,.86f);
        Image panel=MakeImage(settingsOverlay.transform,"Settings Panel",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(560f,380f),new Color(.075f,.11f,.16f,1f));
        panel.raycastTarget=true;
        MakeImage(panel.transform,"Panel accent",new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0f,-2f),new Vector2(560f,3f),accent);
        MakeText(panel.transform,config.settingsTitle,34,TextAnchor.MiddleCenter,new Vector2(.5f,1f),new Vector2(0f,-58f),new Vector2(460f,55f),Color.white,config.buttonFontFamily);
        MakeText(panel.transform,config.volumeLabel,18,TextAnchor.MiddleLeft,new Vector2(.5f,.5f),new Vector2(-150f,47f),new Vector2(270f,30f),new Color(.83f,.89f,.94f),config.buttonFontFamily);
        BuildVolumeSlider(panel.transform);
        BuildFullscreenToggle(panel.transform);
        MakeButton(panel.transform,config.backLabel,new Vector2(.5f,0f),new Vector2(0f,40f),new Vector2(230f,52f),accent,config.buttonFontSize,config.buttonFontFamily,CloseSettings);
        settingsOverlay.SetActive(false);
    }

    void BuildVolumeSlider(Transform parent)
    {
        Image track=MakeImage(parent,"Volume Slider",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(35f,5f),new Vector2(250f,16f),new Color(.13f,.2f,.27f));
        Image fill=MakeImage(track.transform,"Fill",Vector2.zero,new Vector2(1f,1f),Vector2.zero,Vector2.zero,new Color(accent.r,accent.g,accent.b,.72f));
        Image handle=MakeImage(track.transform,"Handle",new Vector2(0f,.5f),new Vector2(0f,.5f),Vector2.zero,new Vector2(24f,24f),Color.white);
        volumeSlider=track.gameObject.AddComponent<Slider>();volumeSlider.fillRect=fill.rectTransform;volumeSlider.handleRect=handle.rectTransform;volumeSlider.targetGraphic=handle;
        volumeSlider.direction=Slider.Direction.LeftToRight;volumeSlider.minValue=0f;volumeSlider.maxValue=1f;volumeSlider.value=AudioListener.volume;
        volumeValue=MakeText(parent,Mathf.RoundToInt(volumeSlider.value*100f)+"%",15,TextAnchor.MiddleRight,new Vector2(.5f,.5f),new Vector2(205f,5f),new Vector2(55f,28f),new Color(.73f,.83f,.9f));
        volumeSlider.onValueChanged.AddListener(value=>{AudioListener.volume=value;volumeValue.text=Mathf.RoundToInt(value*100f)+"%";});
    }

    void BuildFullscreenToggle(Transform parent)
    {
        var toggleObject=new GameObject("Fullscreen Toggle",typeof(RectTransform),typeof(Image),typeof(Toggle));toggleObject.transform.SetParent(parent,false);
        RectTransform rect=toggleObject.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-166f,-52f);rect.sizeDelta=new Vector2(32f,32f);
        Image background=toggleObject.GetComponent<Image>();background.color=new Color(.13f,.2f,.27f);Toggle toggle=toggleObject.GetComponent<Toggle>();toggle.targetGraphic=background;
        Image check=MakeImage(toggleObject.transform,"Check",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(16f,16f),accent);toggle.graphic=check;
        fullscreenToggle=toggle;fullscreenToggle.isOn=Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(value=>Screen.fullScreen=value);
        MakeText(parent,config.fullscreenLabel,18,TextAnchor.MiddleLeft,new Vector2(.5f,.5f),new Vector2(45f,-52f),new Vector2(260f,34f),new Color(.83f,.89f,.94f),config.buttonFontFamily);
    }

    void StartGame(){SceneManager.LoadScene("InPlay");}
    void OpenSettings(){settingsOverlay.SetActive(true);}
    void CloseSettings(){settingsOverlay.SetActive(false);}
    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#endif
    }

    void BuildCustomTextBoxes(Transform canvas)
    {
        if(config.textBoxes==null)return;
        foreach(TextBoxConfig box in config.textBoxes)
        {
            if(box==null||string.IsNullOrWhiteSpace(box.text))continue;
            float x=Mathf.Clamp(box.x,0f,100f),y=Mathf.Clamp(box.y,0f,100f);
            float width=Mathf.Clamp(box.width,5f,100f),height=Mathf.Clamp(box.height,4f,100f);
            int fontSize=Mathf.Clamp(box.fontSize,8,120);
            Color color;if(!ColorUtility.TryParseHtmlString(box.color,out color))color=Color.white;
            TextAnchor alignment=box.alignment=="Left"?TextAnchor.MiddleLeft:box.alignment=="Right"?TextAnchor.MiddleRight:TextAnchor.MiddleCenter;
            Text text=MakeText(canvas,box.text,fontSize,alignment,new Vector2(x*.01f,1f-y*.01f),Vector2.zero,new Vector2(width*12.8f,height*7.2f),color,box.fontFamily);
            text.gameObject.name="Custom Text Box";
        }
    }

    static Button MakeButton(Transform parent,string label,Vector2 anchor,Vector2 position,Vector2 size,Color baseColor,int fontSize,string fontFamily,UnityEngine.Events.UnityAction action)
    {
        var buttonObject=new GameObject(label+" Button",typeof(RectTransform),typeof(Image),typeof(Button));buttonObject.transform.SetParent(parent,false);
        RectTransform rect=buttonObject.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        Image image=buttonObject.GetComponent<Image>();image.color=new Color(baseColor.r*.22f,baseColor.g*.28f,baseColor.b*.32f,.96f);
        Button button=buttonObject.GetComponent<Button>();button.targetGraphic=image;ColorBlock colors=button.colors;colors.normalColor=image.color;colors.highlightedColor=new Color(baseColor.r,baseColor.g,baseColor.b,.46f);colors.pressedColor=baseColor;colors.selectedColor=colors.highlightedColor;button.colors=colors;button.onClick.AddListener(action);
        MakeImage(buttonObject.transform,"Accent edge",new Vector2(0f,0f),new Vector2(0f,1f),new Vector2(2f,0f),new Vector2(3f,0f),baseColor);
        MakeText(buttonObject.transform,label,fontSize,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),Vector2.zero,size,Color.white,fontFamily);
        return button;
    }

    static Image MakeImage(Transform parent,string name,Vector2 anchorMin,Vector2 anchorMax,Vector2 position,Vector2 size,Color color)
    {
        var imageObject=new GameObject(name,typeof(RectTransform),typeof(Image));imageObject.transform.SetParent(parent,false);
        RectTransform rect=imageObject.GetComponent<RectTransform>();rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=new Vector2((anchorMin.x+anchorMax.x)*.5f,(anchorMin.y+anchorMax.y)*.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        Image image=imageObject.GetComponent<Image>();image.color=color;return image;
    }

    static Text MakeText(Transform parent,string value,int size,TextAnchor alignment,Vector2 anchor,Vector2 position,Vector2 dimensions,Color color,string fontFamily="Arial")
    {
        var textObject=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));textObject.transform.SetParent(parent,false);
        RectTransform rect=textObject.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=dimensions;
        Text text=textObject.GetComponent<Text>();text.text=value;text.font=GetFont(fontFamily);text.fontSize=size;text.fontStyle=FontStyle.Bold;text.alignment=alignment;text.color=color;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
        return text;
    }

    static Font GetFont(string family)
    {
        if(string.IsNullOrWhiteSpace(family))family="Arial";
        if(FontCache.TryGetValue(family,out Font cached)&&cached)return cached;
        Font font=null;
        try{font=Font.CreateDynamicFontFromOSFont(family,32);}catch(Exception){ }
        if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        FontCache[family]=font;return font;
    }
}

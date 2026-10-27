using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LADIDMainMenu : MonoBehaviour
{
    [Serializable]
    public sealed class MenuConfig
    {
        public string accentColor="#55D6FF";
        public MenuElementConfig[] elements=new MenuElementConfig[0];
        public LegacyTextBox[] textBoxes=new LegacyTextBox[0];
        public string title="LADID",subtitle="LIFE AND DEATH IN DIMENSION",startLabel="게임 시작",settingsLabel="설정",quitLabel="게임 나가기";
        public string settingsTitle="설정",volumeLabel="마스터 볼륨",fullscreenLabel="전체 화면",backLabel="뒤로";
        public int titleFontSize=78,subtitleFontSize=18,buttonFontSize=20;
        public string titleFontFamily="Arial",subtitleFontFamily="Arial",buttonFontFamily="Malgun Gothic";
    }

    [Serializable]
    public sealed class MenuElementConfig
    {
        public string id="",label="",type="text",group="main",parentId="",text="";
        public float x=50f,y=50f,width=30f,height=8f;
        public int layer=1,fontSize=20;
        public string fontFamily="Arial",fontStyle="Bold",textColor="#FFFFFF",backgroundColor="#000000",alignment="Center",verticalAlignment="Middle";
        public float backgroundAlpha=0f;
        public bool visible=true,interactable=false,useAccent=false;
    }

    [Serializable]
    public sealed class LegacyTextBox
    {
        public string text="";public float x=50f,y=50f,width=36f,height=10f;public int fontSize=24;
        public string fontFamily="Arial",color="#FFFFFF",alignment="Center";
    }

    static readonly Dictionary<string,Font> FontCache=new Dictionary<string,Font>();
    MenuConfig config;
    readonly Dictionary<string,MenuElementConfig> elements=new Dictionary<string,MenuElementConfig>();
    GameObject settingsRoot;
    Slider volumeSlider;
    Toggle fullscreenToggle;
    Image fullscreenCheck;
    Text volumeValue;
    string volumeValueTemplate="{value}%";
    Color accent;

    void Start()
    {
        Application.targetFrameRate=60;config=LoadConfig();
        if(!ColorUtility.TryParseHtmlString(config.accentColor,out accent))accent=new Color(.33f,.84f,1f);
        foreach(MenuElementConfig element in config.elements)if(element!=null&&!string.IsNullOrEmpty(element.id))elements[element.id]=element;
        EnsureEventSystem();BuildMenu();
    }

    MenuConfig LoadConfig()
    {
        var defaults=CreateDefaultElements();string path=Path.Combine(Application.streamingAssetsPath,"main-menu.json");
        if(!File.Exists(path))return new MenuConfig{elements=defaults.ToArray()};
        try
        {
            MenuConfig loaded=JsonUtility.FromJson<MenuConfig>(File.ReadAllText(path));
            if(loaded==null)return new MenuConfig{elements=defaults.ToArray()};
            if(loaded.elements!=null&&loaded.elements.Length>0)
            {
                var merged=new List<MenuElementConfig>(defaults);
                foreach(MenuElementConfig item in loaded.elements)
                {
                    if(item==null||string.IsNullOrWhiteSpace(item.id))continue;
                    int index=merged.FindIndex(existing=>existing.id==item.id);
                    if(index<0)merged.Add(item);else merged[index]=CompleteElement(item,merged[index]);
                }
                loaded.elements=merged.ToArray();return loaded;
            }

            ApplyLegacyConfig(loaded,defaults);
            if(loaded.textBoxes!=null)foreach(LegacyTextBox box in loaded.textBoxes)
            {
                if(box==null||string.IsNullOrWhiteSpace(box.text))continue;
                defaults.Add(new MenuElementConfig{id="custom_legacy_"+defaults.Count,label="추가 텍스트",type="text",group="main",text=box.text,x=box.x,y=box.y,width=box.width,height=box.height,fontSize=box.fontSize,fontFamily=box.fontFamily,textColor=box.color,alignment=box.alignment,verticalAlignment="Middle",backgroundColor="#000000",backgroundAlpha=0f,visible=true,layer=25});
            }
            loaded.elements=defaults.ToArray();return loaded;
        }
        catch(Exception exception)
        {
            Debug.LogWarning("Could not read main-menu.json. Using built-in menu defaults. "+exception.Message);
            return new MenuConfig{elements=defaults.ToArray()};
        }
    }

    static MenuElementConfig CompleteElement(MenuElementConfig item,MenuElementConfig fallback)
    {
        if(string.IsNullOrEmpty(item.label))item.label=fallback.label;
        if(string.IsNullOrEmpty(item.type))item.type=fallback.type;
        if(string.IsNullOrEmpty(item.group))item.group=fallback.group;
        if(string.IsNullOrEmpty(item.parentId))item.parentId=fallback.parentId;
        if(item.width<=0f)item.width=fallback.width;if(item.height<=0f)item.height=fallback.height;
        if(item.fontSize<=0)item.fontSize=fallback.fontSize;
        if(string.IsNullOrEmpty(item.fontFamily))item.fontFamily=fallback.fontFamily;
        if(string.IsNullOrEmpty(item.fontStyle))item.fontStyle=fallback.fontStyle;
        if(string.IsNullOrEmpty(item.textColor))item.textColor=fallback.textColor;
        if(string.IsNullOrEmpty(item.backgroundColor))item.backgroundColor=fallback.backgroundColor;
        if(string.IsNullOrEmpty(item.alignment))item.alignment=fallback.alignment;
        if(string.IsNullOrEmpty(item.verticalAlignment))item.verticalAlignment=fallback.verticalAlignment;
        return item;
    }

    static void ApplyLegacyConfig(MenuConfig old,List<MenuElementConfig> target)
    {
        CopyLegacyText(target,"title",old.title,old.titleFontSize,old.titleFontFamily);
        CopyLegacyText(target,"subtitle",old.subtitle,old.subtitleFontSize,old.subtitleFontFamily);
        CopyLegacyText(target,"startButtonText",old.startLabel,old.buttonFontSize,old.buttonFontFamily);
        CopyLegacyText(target,"settingsButtonText",old.settingsLabel,old.buttonFontSize,old.buttonFontFamily);
        CopyLegacyText(target,"quitButtonText",old.quitLabel,old.buttonFontSize,old.buttonFontFamily);
        CopyLegacyText(target,"settingsTitle",old.settingsTitle,34,old.buttonFontFamily);
        CopyLegacyText(target,"volumeLabel",old.volumeLabel,18,old.buttonFontFamily);
        CopyLegacyText(target,"fullscreenLabel",old.fullscreenLabel,18,old.buttonFontFamily);
        CopyLegacyText(target,"backButtonText",old.backLabel,old.buttonFontSize,old.buttonFontFamily);
    }
    static void CopyLegacyText(List<MenuElementConfig> list,string id,string text,int size,string font)
    {
        MenuElementConfig element=list.Find(item=>item.id==id);if(element==null)return;
        element.text=text;element.fontSize=size;element.fontFamily=font;
    }

    static List<MenuElementConfig> CreateDefaultElements()
    {
        return new List<MenuElementConfig>
        {
            E("background","배경","image","main","",50,50,100,100,0,bg:"#101923",alpha:1),
            E("topAccent","상단 강조선","image","main","",50,1,23.4f,.42f,2,useAccent:true,alpha:1),
            E("title","게임 제목","text","main","LADID",50,28.75f,59.4f,13.9f,4,78,"Arial"),
            E("subtitle","부제목","text","main","LIFE AND DEATH IN DIMENSION",50,35.7f,59.4f,4.7f,5,18,"Arial",color:"#ABC2D6"),
            E("titleUnderline","제목 밑줄","image","main","",50,39.3f,5.94f,.28f,3,useAccent:true,alpha:.75f),
            E("startButton","게임 시작 버튼 배경","button","main","",50,48.1f,27.34f,8.06f,10,bg:"#124057",alpha:.96f,click:true),
            E("startButtonAccent","시작 버튼 강조선","image","main","",36.45f,48.1f,.24f,8.06f,11,useAccent:true,alpha:1),
            E("startButtonText","게임 시작 글자","text","main","게임 시작",50,48.1f,27.34f,8.06f,12,20,"Malgun Gothic"),
            E("settingsButton","설정 버튼 배경","button","main","",50,58.6f,27.34f,8.06f,10,bg:"#080F17",alpha:.96f,click:true),
            E("settingsButtonAccent","설정 버튼 강조선","image","main","",36.45f,58.6f,.24f,8.06f,11,bg:"#25394A",alpha:1),
            E("settingsButtonText","설정 버튼 글자","text","main","설정",50,58.6f,27.34f,8.06f,12,20,"Malgun Gothic"),
            E("quitButton","나가기 버튼 배경","button","main","",50,69.2f,27.34f,8.06f,10,bg:"#080F17",alpha:.96f,click:true),
            E("quitButtonAccent","나가기 버튼 강조선","image","main","",36.45f,69.2f,.24f,8.06f,11,bg:"#25394A",alpha:1),
            E("quitButtonText","나가기 버튼 글자","text","main","게임 나가기",50,69.2f,27.34f,8.06f,12,20,"Malgun Gothic"),
            E("footer","하단 설명","text","main","A SURVIVAL JOURNEY THROUGH COLLAPSING DIMENSIONS",50,96.7f,70.3f,3.3f,3,12,"Arial",color:"#6E8A9E"),

            E("settingsOverlay","설정창 배경 어둡게","image","settings","",50,50,100,100,0,bg:"#04070A",alpha:.86f),
            E("settingsPanel","설정 패널","image","settings","",50,50,43.75f,52.8f,1,bg:"#131C29",alpha:1),
            E("settingsAccent","설정 패널 상단선","image","settings","",50,24,43.75f,.42f,2,useAccent:true,alpha:1),
            E("settingsTitle","설정 제목","text","settings","설정",50,31.7f,35.9f,7.6f,3,34,"Malgun Gothic"),
            E("volumeLabel","볼륨 설명","text","settings","마스터 볼륨",38.3f,43.5f,21.1f,4.2f,3,18,"Malgun Gothic",align:"Left",color:"#D4E3EF"),
            E("volumeSlider","볼륨 슬라이더","slider","settings","",52.7f,49.3f,19.5f,2.22f,4,bg:"#212F3D",alpha:1,click:true),
            E("volumeSliderFill","볼륨 슬라이더 채움","sliderFill","settings","",0,50,100,42,5,useAccent:true,alpha:.72f,parent:"volumeSlider"),
            E("volumeSliderHandle","볼륨 슬라이더 손잡이","sliderHandle","settings","",100,50,9.6f,145,6,bg:"#FFFFFF",alpha:1,parent:"volumeSlider"),
            E("volumeValue","볼륨 값","text","settings","{value}%",66f,49.3f,4.3f,3.9f,7,15,"Arial",align:"Right",color:"#BACBD7"),
            E("fullscreenToggle","전체 화면 토글","toggle","settings","",37,57.2f,2.5f,4.4f,8,bg:"#212F3D",alpha:1,click:true),
            E("fullscreenCheck","전체 화면 체크 표시","image","settings","",37,57.2f,1.25f,2.2f,9,useAccent:true,alpha:1),
            E("fullscreenLabel","전체 화면 설명","text","settings","전체 화면",53.5f,57.2f,20.3f,4.7f,9,18,"Malgun Gothic",align:"Left",color:"#D4E3EF"),
            E("backButton","뒤로 버튼 배경","button","settings","",50,90.8f,18f,7.2f,10,bg:"#124057",alpha:.96f,click:true),
            E("backButtonAccent","뒤로 버튼 강조선","image","settings","",41.1f,90.8f,.24f,7.2f,11,useAccent:true,alpha:1),
            E("backButtonText","뒤로 버튼 글자","text","settings","뒤로",50,90.8f,18f,7.2f,12,20,"Malgun Gothic"),
        };
    }

    static MenuElementConfig E(string id,string label,string type,string group,string text,float x,float y,float width,float height,int layer,int fontSize=20,string font="Arial",string color="#FFFFFF",string bg="#000000",float alpha=0f,string align="Center",bool useAccent=false,bool click=false,string parent="")
    {
        return new MenuElementConfig{id=id,label=label,type=type,group=group,text=text,x=x,y=y,width=width,height=height,layer=layer,fontSize=fontSize,fontFamily=font,fontStyle="Bold",textColor=color,backgroundColor=bg,backgroundAlpha=alpha,alignment=align,verticalAlignment="Middle",visible=true,interactable=click,useAccent=useAccent,parentId=parent};
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem=EventSystem.current?EventSystem.current:FindObjectOfType<EventSystem>();
        if(!eventSystem)
        {
            var events=new GameObject("EventSystem",typeof(EventSystem));DontDestroyOnLoad(events);
            eventSystem=events.GetComponent<EventSystem>();
        }
        StandaloneInputModule inputModule=eventSystem.GetComponent<StandaloneInputModule>();
        if(!inputModule)inputModule=eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        inputModule.enabled=true;
    }

    void BuildMenu()
    {
        EnsureMenuCamera();
        var canvasObject=new GameObject("Main Menu Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        Canvas canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
        CanvasScaler scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        settingsRoot=new GameObject("Settings Group",typeof(RectTransform));settingsRoot.transform.SetParent(canvas.transform,false);Stretch(settingsRoot.GetComponent<RectTransform>());

        foreach(MenuElementConfig element in config.elements.Where(item=>item!=null).OrderBy(item=>item.group=="settings"?1:0).ThenBy(item=>item.layer))
        {
            Transform parent=element.group=="settings"?settingsRoot.transform:canvas.transform;
            if(element.type=="image")CreateImageElement(parent,element);
            else if(element.type=="text")CreateTextElement(parent,element);
            else if(element.type=="button")CreateButtonElement(parent,element);
            else if(element.type=="slider")CreateSliderElement(parent,element);
            else if(element.type=="toggle")CreateToggleElement(parent,element);
        }
        if(fullscreenToggle&&fullscreenCheck)fullscreenToggle.graphic=fullscreenCheck;
        UpdateVolumeDisplay(AudioListener.volume);settingsRoot.SetActive(false);
    }

    void EnsureMenuCamera()
    {
        Camera menuCamera=FindObjectOfType<Camera>();
        if(!menuCamera)
        {
            GameObject cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.position=new Vector3(0f,0f,-10f);
            menuCamera=cameraObject.AddComponent<Camera>();menuCamera.orthographic=true;menuCamera.clearFlags=CameraClearFlags.SolidColor;menuCamera.backgroundColor=ParseColor("#101923",Color.black);
        }
        if(!FindObjectOfType<AudioListener>())menuCamera.gameObject.AddComponent<AudioListener>();
    }

    void CreateImageElement(Transform parent,MenuElementConfig element)
    {
        GameObject imageObject=new GameObject(element.label,typeof(RectTransform),typeof(Image));imageObject.transform.SetParent(parent,false);
        ApplyRect(imageObject.GetComponent<RectTransform>(),element);Image image=imageObject.GetComponent<Image>();image.color=GetBackgroundColor(element);image.raycastTarget=element.id=="settingsOverlay";
        if(element.id=="fullscreenCheck")fullscreenCheck=image;
        imageObject.SetActive(element.visible);
    }

    void CreateTextElement(Transform parent,MenuElementConfig element)
    {
        GameObject box=new GameObject(element.label,typeof(RectTransform),typeof(Image));box.transform.SetParent(parent,false);
        ApplyRect(box.GetComponent<RectTransform>(),element);Image background=box.GetComponent<Image>();background.color=GetBackgroundColor(element);background.raycastTarget=false;
        GameObject textObject=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));textObject.transform.SetParent(box.transform,false);Stretch(textObject.GetComponent<RectTransform>());
        Text text=textObject.GetComponent<Text>();text.text=element.text;text.font=GetFont(element.fontFamily);text.fontSize=Mathf.Clamp(element.fontSize,8,120);text.fontStyle=ParseFontStyle(element.fontStyle);text.alignment=ParseAlignment(element.alignment,element.verticalAlignment);text.color=ParseColor(element.textColor,Color.white);text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
        if(element.id=="volumeValue"){volumeValue=text;volumeValueTemplate=element.text;}
        box.SetActive(element.visible);
    }

    void CreateButtonElement(Transform parent,MenuElementConfig element)
    {
        GameObject buttonObject=new GameObject(element.label,typeof(RectTransform),typeof(Image),typeof(Button));buttonObject.transform.SetParent(parent,false);
        ApplyRect(buttonObject.GetComponent<RectTransform>(),element);Image image=buttonObject.GetComponent<Image>();image.color=GetBackgroundColor(element);
        image.raycastTarget=true;
        Button button=buttonObject.GetComponent<Button>();button.enabled=true;button.targetGraphic=image;button.interactable=element.interactable;
        ColorBlock colors=button.colors;colors.normalColor=image.color;colors.highlightedColor=Color.Lerp(image.color,accent,.3f);colors.pressedColor=Color.Lerp(image.color,accent,.55f);colors.selectedColor=colors.highlightedColor;button.colors=colors;
        if(element.id=="startButton")button.onClick.AddListener(StartGame);
        else if(element.id=="settingsButton")button.onClick.AddListener(OpenSettings);
        else if(element.id=="quitButton")button.onClick.AddListener(QuitGame);
        else if(element.id=="backButton")button.onClick.AddListener(CloseSettings);
        buttonObject.SetActive(element.visible);
    }

    void CreateSliderElement(Transform parent,MenuElementConfig element)
    {
        GameObject sliderObject=new GameObject(element.label,typeof(RectTransform),typeof(Image),typeof(Slider));sliderObject.transform.SetParent(parent,false);
        RectTransform rootRect=sliderObject.GetComponent<RectTransform>();ApplyRect(rootRect,element);
        Image track=sliderObject.GetComponent<Image>();track.color=GetBackgroundColor(element);track.raycastTarget=true;
        Slider slider=sliderObject.GetComponent<Slider>();slider.direction=Slider.Direction.LeftToRight;slider.minValue=0f;slider.maxValue=1f;slider.interactable=element.interactable;
        MenuElementConfig fillConfig=FindElement("volumeSliderFill"),handleConfig=FindElement("volumeSliderHandle");
        Image fill=CreateSliderPart(sliderObject.transform,fillConfig,rootRect,false);
        Image handle=CreateSliderPart(sliderObject.transform,handleConfig,rootRect,true);
        if(fillConfig!=null&&handleConfig!=null&&handleConfig.layer<fillConfig.layer)handle.transform.SetAsFirstSibling();
        slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
        slider.value=AudioListener.volume;slider.onValueChanged.AddListener(value=>{AudioListener.volume=value;UpdateVolumeDisplay(value);});
        if(fillConfig!=null)fill.gameObject.SetActive(fillConfig.visible);if(handleConfig!=null)handle.gameObject.SetActive(handleConfig.visible);
        sliderObject.SetActive(element.visible);volumeSlider=slider;
    }

    Image CreateSliderPart(Transform parent,MenuElementConfig element,RectTransform root,bool handle)
    {
        if(element==null)element=new MenuElementConfig{visible=true,backgroundColor="#FFFFFF",backgroundAlpha=1f,x=0f,y=50f,width=100f,height=40f};
        GameObject part=new GameObject(element.label,typeof(RectTransform),typeof(Image));part.transform.SetParent(parent,false);
        RectTransform rect=part.GetComponent<RectTransform>();Image image=part.GetComponent<Image>();image.color=GetBackgroundColor(element);image.raycastTarget=handle;
        float rootWidth=element==null?250f:FindElement("volumeSlider").width*12.8f;float rootHeight=FindElement("volumeSlider").height*7.2f;
        float y=1f-element.y*.01f,halfHeight=element.height*.005f;
        if(handle)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0f,y);rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(rootWidth*element.width*.01f,rootHeight*element.height*.01f);
        }
        else
        {
            rect.anchorMin=new Vector2(element.x*.01f,y-halfHeight);rect.anchorMax=new Vector2((element.x+element.width)*.01f,y+halfHeight);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        return image;
    }

    void CreateToggleElement(Transform parent,MenuElementConfig element)
    {
        GameObject toggleObject=new GameObject(element.label,typeof(RectTransform),typeof(Image),typeof(Toggle));toggleObject.transform.SetParent(parent,false);
        ApplyRect(toggleObject.GetComponent<RectTransform>(),element);Image image=toggleObject.GetComponent<Image>();image.color=GetBackgroundColor(element);
        Toggle toggle=toggleObject.GetComponent<Toggle>();toggle.targetGraphic=image;toggle.interactable=element.interactable;toggle.isOn=Screen.fullScreen;toggle.onValueChanged.AddListener(value=>Screen.fullScreen=value);
        toggleObject.SetActive(element.visible);fullscreenToggle=toggle;
    }

    MenuElementConfig FindElement(string id)
    {
        if(elements.TryGetValue(id,out MenuElementConfig element))return element;
        return null;
    }
    Color GetBackgroundColor(MenuElementConfig element)
    {
        Color color=element.useAccent?accent:ParseColor(element.backgroundColor,Color.clear);color.a*=element.backgroundAlpha;return color;
    }
    void UpdateVolumeDisplay(float value)
    {
        if(volumeValue)volumeValue.text=volumeValueTemplate.Replace("{value}",Mathf.RoundToInt(value*100f).ToString());
    }

    void StartGame(){SceneManager.LoadScene("InPlay");}
    void OpenSettings(){if(settingsRoot)settingsRoot.SetActive(true);}
    void CloseSettings(){if(settingsRoot)settingsRoot.SetActive(false);}
    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#endif
    }

    static Color ParseColor(string value,Color fallback)
    {
        if(ColorUtility.TryParseHtmlString(value,out Color parsed))return parsed;
        return fallback;
    }
    static Font GetFont(string family)
    {
        if(string.IsNullOrWhiteSpace(family))family="Arial";
        if(FontCache.TryGetValue(family,out Font cached)&&cached)return cached;
        Font font=null;try{font=Font.CreateDynamicFontFromOSFont(family,32);}catch(Exception){ }
        if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");FontCache[family]=font;return font;
    }
    static FontStyle ParseFontStyle(string value)
    {
        if(value=="Italic")return FontStyle.Italic;if(value=="BoldAndItalic")return FontStyle.BoldAndItalic;if(value=="Normal")return FontStyle.Normal;return FontStyle.Bold;
    }
    static TextAnchor ParseAlignment(string horizontal,string vertical)
    {
        if(vertical=="Top")return horizontal=="Left"?TextAnchor.UpperLeft:horizontal=="Right"?TextAnchor.UpperRight:TextAnchor.UpperCenter;
        if(vertical=="Bottom")return horizontal=="Left"?TextAnchor.LowerLeft:horizontal=="Right"?TextAnchor.LowerRight:TextAnchor.LowerCenter;
        return horizontal=="Left"?TextAnchor.MiddleLeft:horizontal=="Right"?TextAnchor.MiddleRight:TextAnchor.MiddleCenter;
    }
    static void ApplyRect(RectTransform rect,MenuElementConfig element)
    {
        Vector2 anchor=new Vector2(Mathf.Clamp01(element.x*.01f),1f-Mathf.Clamp01(element.y*.01f));rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(element.width*12.8f,element.height*7.2f);
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);
    }
}

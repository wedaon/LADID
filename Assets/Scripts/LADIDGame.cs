using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class LADIDGame : MonoBehaviour
{
    const float W = 8f, H = 5f;
    enum World { Volcano, Tidal, Earthquake, AcidRain, Darkness }
    sealed class FissureData
    {
        public bool triggered;
        public int side;
        public List<Vector2> path;
    }
    sealed class LightZone
    {
        public Vector2 position;
        public float radius;
        public GameObject glow, source;
    }
    Transform player;
    Transform hazardsRoot;
    Transform splitVisualRoot;
    SpriteRenderer leftFieldRenderer, rightFieldRenderer;
    readonly SpriteRenderer[] fieldEdges=new SpriteRenderer[4];
    SpriteRenderer playerRenderer;
    SpriteRenderer playerAura;
    Color playerColor = new Color(.88f,.96f,1f);
    Camera cam;
    ParticleSystem atmosphere;
    Text worldText, timerText, livesText, messageText, stageText;
    Image banner, flash, shatter, darknessOverlay;
    Texture2D darknessTexture;
    Color32[] darknessPixels;
    World world;
    World secondaryWorld;
    readonly List<FissureData> fissures = new List<FissureData>();
    readonly List<LightZone> lightZones = new List<LightZone>();
    float phaseTime, phaseDuration, survived, flashTime, damageTintTime, cameraShake, health = 100f;
    const int DarknessMaskWidth=192, DarknessMaskHeight=108;
    int stage = 1;
    bool ended, changing, falling, overlapMode;

    void Start()
    {
        Application.targetFrameRate = 60;
        hazardsRoot = new GameObject("Dimension Hazards").transform;
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 6.5f; cam.transform.position = new Vector3(0,0,-10);
        cam.backgroundColor = new Color(.12f,.09f,.1f);
        leftFieldRenderer=Rect("Field Left",new Vector3(-4,0),8,10,new Color(.2f,.19f,.19f),0).GetComponent<SpriteRenderer>();
        rightFieldRenderer=Rect("Field Right",new Vector3(4,0),8,10,new Color(.2f,.19f,.19f),0).GetComponent<SpriteRenderer>();
        for (int i=-7;i<=7;i++) Rect("Grid",new Vector3(i,0,.1f),.018f,10,new Color(1,1,1,.04f),1);
        for (int i=-4;i<=4;i++) Rect("Grid",new Vector3(0,i,.1f),16,.018f,new Color(1,1,1,.04f),1);
        fieldEdges[0]=Rect("Top edge",new Vector3(0,5.1f),16.3f,.2f,new Color(1,.48f,.18f),1).GetComponent<SpriteRenderer>();
        fieldEdges[1]=Rect("Bottom edge",new Vector3(0,-5.1f),16.3f,.2f,new Color(1,.48f,.18f),1).GetComponent<SpriteRenderer>();
        fieldEdges[2]=Rect("Left edge",new Vector3(-8.1f,0),.2f,10.3f,new Color(1,.48f,.18f),1).GetComponent<SpriteRenderer>();
        fieldEdges[3]=Rect("Right edge",new Vector3(8.1f,0),.2f,10.3f,new Color(1,.48f,.18f),1).GetComponent<SpriteRenderer>();
        BuildSplitVisual();
        BuildAtmosphere();
        player = Rect("Player",Vector3.zero,.52f,.52f,playerColor,5).transform;
        playerRenderer = player.GetComponent<SpriteRenderer>();
        GameObject aura=new GameObject("Player aura");aura.transform.SetParent(player,false);aura.transform.localScale=Vector3.one*3.4f;
        playerAura=aura.AddComponent<SpriteRenderer>();playerAura.sprite=Sprites.Circle;playerAura.color=new Color(.25f,.78f,1f,.18f);playerAura.sortingOrder=4;
        BuildHud();
        world=(World)Random.Range(0,5);secondaryWorld=world;SetWorld(world,true);
        StartCoroutine(PhaseLoop()); StartCoroutine(VolcanoLoop()); StartCoroutine(TideLoop()); StartCoroutine(EarthquakeLoop()); StartCoroutine(AcidRainLoop());
    }

    void Update()
    {
        if (ended) { if (Input.GetKeyDown(KeyCode.R)) Restart(); return; }
        float dt=Time.deltaTime; survived+=dt; phaseTime-=dt;
        Vector2 v=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
        if(v.sqrMagnitude>1) v.Normalize();
        Vector3 p=player.position+(Vector3)(v*7f*dt); p.x=Mathf.Clamp(p.x,-7.65f,7.65f); p.y=Mathf.Clamp(p.y,-4.65f,4.65f); player.position=p;
        timerText.text="NEXT COLLAPSE  "+Mathf.CeilToInt(phaseTime).ToString("00")+"s";
        stageText.text="STAGE  "+stage.ToString("00");
        ApplyCoreContactDamage(dt);
        ApplyFissureContact();
        if(!changing&&IsWorldActive(World.Darkness))
        {
            ApplyDarknessDamage(dt);
            UpdateDarknessMask();
        }
        livesText.text="HEALTH  "+Mathf.CeilToInt(health).ToString("000")+" / 100";
        if(damageTintTime>0){damageTintTime-=dt;playerRenderer.color=new Color(1f,.16f,.16f);}
        else playerRenderer.color=playerColor;
        Color auraColor=damageTintTime>0?new Color(1f,.12f,.08f,.3f):WorldAccentColor(world);auraColor.a=damageTintTime>0?.3f:.16f;playerAura.color=auraColor;
        if(flashTime>0){flashTime-=dt;flash.color=new Color(1,.15f,.1f,Mathf.Clamp01(flashTime*.35f));}
        if(cameraShake>0f){cameraShake=Mathf.MoveTowards(cameraShake,0f,dt*1.4f);Vector2 jitter=Random.insideUnitCircle*cameraShake;cam.transform.position=new Vector3(jitter.x,jitter.y,-10f);}
        else cam.transform.position=new Vector3(0f,0f,-10f);
    }

    void BuildHud()
    {
        var c=new GameObject("HUD").AddComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=20;
        var scaler=c.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); c.gameObject.AddComponent<GraphicRaycaster>();
        Label(c.transform,"LADID  /  LIFE AND DEATH IN DIMENSION",24,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-22),new Vector2(760,40),Color.white);
        worldText=Label(c.transform,"",20,TextAnchor.UpperRight,new Vector2(1,1),new Vector2(1,1),new Vector2(-28,-24),new Vector2(400,36),Color.white);
        timerText=Label(c.transform,"",16,TextAnchor.UpperRight,new Vector2(1,1),new Vector2(1,1),new Vector2(-28,-60),new Vector2(400,28),new Color(1,.75f,.45f));
        livesText=Label(c.transform,"",20,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-72),new Vector2(300,30),Color.white);
        stageText=Label(c.transform,"",16,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-104),new Vector2(300,28),new Color(.75f,.85f,1f));
        messageText=Label(c.transform,"",16,TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(900,44),Color.white);
        banner=Panel(c.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,100),new Color(.4f,.1f,.07f,.94f));
        Label(banner.transform,"",26,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white);
        flash=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Color(1,.1f,.1f,0));
        flash.rectTransform.anchorMax=Vector2.one; flash.rectTransform.offsetMin=Vector2.zero; flash.rectTransform.offsetMax=Vector2.zero;
        shatter=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,Color.white);
        shatter.rectTransform.anchorMax=Vector2.one; shatter.rectTransform.offsetMin=Vector2.zero; shatter.rectTransform.offsetMax=Vector2.zero;
        shatter.rectTransform.pivot=new Vector2(.5f,.5f);shatter.rectTransform.anchoredPosition=Vector2.zero;
        shatter.sprite=CreateCrackSprite(); shatter.type=Image.Type.Simple; shatter.raycastTarget=false; shatter.color=new Color(1,1,1,0);
        darknessTexture=new Texture2D(DarknessMaskWidth,DarknessMaskHeight,TextureFormat.RGBA32,false);darknessTexture.filterMode=FilterMode.Bilinear;darknessTexture.wrapMode=TextureWrapMode.Clamp;
        darknessPixels=new Color32[DarknessMaskWidth*DarknessMaskHeight];for(int i=0;i<darknessPixels.Length;i++)darknessPixels[i]=new Color32(0,0,0,248);
        darknessTexture.SetPixels32(darknessPixels);darknessTexture.Apply();
        darknessOverlay=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,Color.white);
        darknessOverlay.rectTransform.anchorMax=Vector2.one;darknessOverlay.rectTransform.offsetMin=Vector2.zero;darknessOverlay.rectTransform.offsetMax=Vector2.zero;
        darknessOverlay.sprite=Sprite.Create(darknessTexture,new Rect(0,0,DarknessMaskWidth,DarknessMaskHeight),new Vector2(.5f,.5f),100f);darknessOverlay.type=Image.Type.Simple;darknessOverlay.raycastTarget=false;
        banner.gameObject.SetActive(false);flash.transform.SetAsLastSibling();shatter.transform.SetAsLastSibling();shatter.gameObject.SetActive(false);darknessOverlay.transform.SetAsFirstSibling();darknessOverlay.gameObject.SetActive(false);
    }

    IEnumerator PhaseLoop()
    {
        while(!ended){yield return new WaitForSeconds(phaseDuration); if(ended)yield break; changing=true; ClearHazards(); banner.gameObject.SetActive(false); StartCoroutine(PlayShatter(1.5f)); yield return new WaitForSeconds(1.5f); stage++; World next=RandomWorld();float overlapChance=stage<3?0f:Mathf.Min((stage-2)*.1f,.5f);overlapMode=Random.value<overlapChance;secondaryWorld=overlapMode?RandomWorldExcept(next):next;SetWorld(next,false);changing=false;}
    }
    World RandomWorld(){World next=world;while(next==world)next=(World)Random.Range(0,5);return next;}
    World RandomWorldExcept(World excluded){World next=excluded;while(next==excluded)next=(World)Random.Range(0,5);return next;}
    bool IsWorldActive(World candidate){return world==candidate||(overlapMode&&secondaryWorld==candidate);}
    Vector2 RandomWorldPosition(float minX,float maxX,float minY,float maxY)
    {
        return new Vector2(Random.Range(minX,maxX),Random.Range(minY,maxY));
    }
    string WorldName(World dimension)
    {
        if(dimension==World.Volcano)return "VOLCANO";
        if(dimension==World.Tidal)return "TIDAL";
        if(dimension==World.Earthquake)return "EARTHQUAKE";
        if(dimension==World.AcidRain)return "ACID RAIN";
        return "DARKNESS";
    }
    Color WorldFieldColor(World dimension)
    {
        if(dimension==World.Volcano)return new Color(.25f,.13f,.11f);
        if(dimension==World.Tidal)return new Color(.07f,.25f,.32f);
        if(dimension==World.Earthquake)return new Color(.3f,.22f,.14f);
        if(dimension==World.AcidRain)return new Color(.13f,.28f,.1f);
        return new Color(.025f,.03f,.065f);
    }
    Color WorldAccentColor(World dimension)
    {
        if(dimension==World.Volcano)return new Color(1f,.42f,.12f,.95f);
        if(dimension==World.Tidal)return new Color(.2f,.82f,1f,.95f);
        if(dimension==World.Earthquake)return new Color(1f,.62f,.25f,.95f);
        if(dimension==World.AcidRain)return new Color(.55f,1f,.2f,.95f);
        return new Color(.56f,.62f,1f,.95f);
    }
    Vector2 AtmosphereDrift(World dimension)
    {
        if(dimension==World.Volcano)return new Vector2(.05f,.8f);
        if(dimension==World.Tidal)return new Vector2(1.1f,.08f);
        if(dimension==World.Earthquake)return new Vector2(.15f,.12f);
        if(dimension==World.AcidRain)return new Vector2(.08f,-1.4f);
        return new Vector2(.08f,.18f);
    }
    void BuildAtmosphere()
    {
        GameObject particles=new GameObject("Dimension Atmosphere");atmosphere=particles.AddComponent<ParticleSystem>();
        var main=atmosphere.main;main.loop=true;main.playOnAwake=false;main.duration=6f;main.maxParticles=320;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=new ParticleSystem.MinMaxCurve(1.8f,4.2f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(.1f,.45f);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.1f);main.startColor=Color.white;
        var emission=atmosphere.emission;emission.rateOverTime=12f;
        var shape=atmosphere.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(16f,10f,.05f);
        var velocity=atmosphere.velocityOverLifetime;velocity.enabled=true;velocity.x=new ParticleSystem.MinMaxCurve(-.3f,.3f);velocity.y=new ParticleSystem.MinMaxCurve(-.3f,.3f);
        var fade=atmosphere.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(.7f,.14f),new GradientAlphaKey(.55f,.78f),new GradientAlphaKey(0f,1f)});
        fade.color=new ParticleSystem.MinMaxGradient(gradient);
        var particleRenderer=particles.GetComponent<ParticleSystemRenderer>();particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;particleRenderer.sortingOrder=2;
    }
    void ConfigureAtmosphere()
    {
        atmosphere.Clear(true);var main=atmosphere.main;Color first=WorldAccentColor(world),second=overlapMode?WorldAccentColor(secondaryWorld):first;
        main.startColor=new ParticleSystem.MinMaxGradient(first,second);
        Vector2 drift=overlapMode?(AtmosphereDrift(world)+AtmosphereDrift(secondaryWorld))*.5f:AtmosphereDrift(world);
        var velocity=atmosphere.velocityOverLifetime;velocity.x=new ParticleSystem.MinMaxCurve(drift.x-.35f,drift.x+.35f);velocity.y=new ParticleSystem.MinMaxCurve(drift.y-.35f,drift.y+.35f);
        var shape=atmosphere.shape;
        if(overlapMode){shape.position=Vector3.zero;shape.scale=new Vector3(16f,10f,.05f);}
        else if(world==World.Volcano){shape.position=new Vector3(0,-4.8f,0);shape.scale=new Vector3(16f,.4f,.05f);}
        else if(world==World.Tidal){shape.position=new Vector3(-7.8f,0,0);shape.scale=new Vector3(.4f,10f,.05f);}
        else if(world==World.AcidRain){shape.position=new Vector3(0,5.1f,0);shape.scale=new Vector3(16f,.35f,.05f);}
        else {shape.position=Vector3.zero;shape.scale=new Vector3(16f,10f,.05f);}
        float baseRate=world==World.AcidRain?24f:world==World.Tidal?16f:world==World.Volcano?13f:10f;
        var emission=atmosphere.emission;emission.rateOverTime=baseRate+(overlapMode?8f:0f)+Mathf.Min(stage-1,10)*1.2f;
        atmosphere.Play(true);
    }
    void EmitBurst(Vector3 position,Color color,int count,float speed)
    {
        if(!atmosphere)return;
        for(int i=0;i<count;i++)
        {
            float angle=Random.Range(0f,Mathf.PI*2f);float velocity=Random.Range(speed*.35f,speed);
            var burst=new ParticleSystem.EmitParams();burst.position=position;burst.velocity=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*velocity;
            burst.startColor=color;burst.startSize=Random.Range(.045f,.12f);burst.startLifetime=Random.Range(.35f,1.1f);atmosphere.Emit(burst,1);
        }
    }
    void BuildSplitVisual()
    {
        splitVisualRoot=new GameObject("Mirror Dimension Divider").transform;
        GameObject seam=Rect("Mirror seam",new Vector3(0,0,-.15f),.075f,10f,new Color(.72f,.92f,1f,.95f),3);
        seam.transform.SetParent(splitVisualRoot,true);
        for(int i=0;i<7;i++)
        {
            float y=-4.2f+i*1.35f;float direction=i%2==0?1f:-1f;
            GameObject branch=Rect("Mirror fracture",new Vector3(direction*.22f,y,-.15f),.58f,.025f,new Color(.75f,.94f,1f,.85f),3);
            branch.transform.SetParent(splitVisualRoot,true);branch.transform.rotation=Quaternion.Euler(0,0,direction*32f);
            GameObject twig=Rect("Mirror fracture",new Vector3(direction*.42f,y+direction*.16f,-.15f),.31f,.018f,new Color(.65f,.86f,1f,.75f),3);
            twig.transform.SetParent(splitVisualRoot,true);twig.transform.rotation=Quaternion.Euler(0,0,-direction*48f);
        }
        splitVisualRoot.gameObject.SetActive(false);
    }
    IEnumerator PlayShatter(float duration)
    {
        shatter.rectTransform.localScale=Vector3.one*.22f;shatter.gameObject.SetActive(true);float elapsed=0f;
        while(elapsed<duration)
        {
            elapsed+=Time.deltaTime; float progress=Mathf.Clamp01(elapsed/duration);
            float alpha=progress<.12f?Mathf.Lerp(0f,.9f,progress/.12f):Mathf.Lerp(.9f,0f,(progress-.12f)/.88f);
            shatter.rectTransform.localScale=Vector3.one*Mathf.Lerp(.22f,1.4f,progress);
            shatter.color=new Color(1,1,1,alpha); yield return null;
        }
        shatter.color=new Color(1,1,1,0);shatter.rectTransform.localScale=Vector3.one;shatter.gameObject.SetActive(false);
    }
    void SetWorld(World next,bool first)
    {
        if(!first) ClearHazards();
        world=next; phaseDuration=Random.Range(10f,20f); phaseTime=phaseDuration;
        bool darknessActive=IsWorldActive(World.Darkness);darknessOverlay.gameObject.SetActive(darknessActive);
        if(darknessActive)CreateDarknessLights();
        ConfigureAtmosphere();
        splitVisualRoot.gameObject.SetActive(overlapMode);
        Color edgeColor=WorldAccentColor(world);foreach(SpriteRenderer edge in fieldEdges)edge.color=edgeColor;
        Color auraTint=WorldAccentColor(world);auraTint.a=.16f;playerAura.color=auraTint;
        if(overlapMode)
        {
            leftFieldRenderer.color=WorldFieldColor(world);rightFieldRenderer.color=WorldFieldColor(secondaryWorld);cam.backgroundColor=new Color(.035f,.035f,.05f);
            worldText.text="OVERLAP  /  "+WorldName(world)+"  +  "+WorldName(secondaryWorld);
            messageText.text=WorldName(world)+" + "+WorldName(secondaryWorld)+"  •  BOTH PATTERNS ACTIVE ACROSS THE FIELD";
        }
        else
        {
            leftFieldRenderer.color=rightFieldRenderer.color=Color.Lerp(new Color(.2f,.19f,.19f),WorldFieldColor(next),.72f);
            cam.backgroundColor=next==World.Volcano?new Color(.12f,.09f,.1f):next==World.Tidal?new Color(.04f,.19f,.27f):next==World.Earthquake?new Color(.16f,.12f,.09f):next==World.AcidRain?new Color(.08f,.16f,.09f):new Color(.01f,.015f,.035f);
            worldText.text=next==World.Volcano?"01  /  VOLCANIC WASTELAND":next==World.Tidal?"02  /  TIDAL WORLD":next==World.Earthquake?"03  /  FRACTURED EARTH":next==World.AcidRain?"04  /  ACID RAIN":"05  /  DARKNESS";
            messageText.text=next==World.Volcano?"VOLCANIC WORLD  •  MOVE OUT OF THE RED WARNING CIRCLES":next==World.Tidal?"TIDAL WORLD  •  EVADE THE HORIZONTAL WAVE BY MOVING UP OR DOWN":next==World.Earthquake?"EARTHQUAKE WORLD  •  LEAVE THE CRACKING GROUND BEFORE IT OPENS":next==World.AcidRain?"ACID RAIN WORLD  •  DODGE THE FALLING DROPS":"DARKNESS WORLD  •  STAY INSIDE THE LIGHT";
        }
        banner.gameObject.SetActive(false);
    }
    IEnumerator HideBanner(){yield return new WaitForSeconds(1.1f);if(!changing)banner.gameObject.SetActive(false);}
    IEnumerator VolcanoLoop(){while(true){yield return new WaitForSeconds(Random.Range(.65f,1.2f));if(ended||changing||!IsWorldActive(World.Volcano))continue;int projectileCount=Mathf.Min(stage,6);for(int i=0;i<projectileCount;i++)StartCoroutine(Meteor(RandomWorldPosition(-7.3f,7.3f,-4.3f,4.3f)));}}
    IEnumerator Meteor(Vector2 at)
    {
        float impactSize=Random.Range(1.2f,3.6f);
        GameObject warn=Disc("Impact warning",at,impactSize,new Color(1,.12f,.04f,.4f),2);
        SpriteRenderer warningRenderer=warn.GetComponent<SpriteRenderer>();float t=0;while(t<1.05f){if(!warn||ended||changing||!IsWorldActive(World.Volcano)){if(warn)Destroy(warn);yield break;}t+=Time.deltaTime;Color warningColor=warningRenderer.color;warningColor.a=.24f+Mathf.PingPong(Time.time*1.8f,.28f);warningRenderer.color=warningColor;yield return null;}
        if(!warn||ended||changing||!IsWorldActive(World.Volcano))yield break;
        Destroy(warn);GameObject hit=Disc("Meteor impact",at,impactSize,new Color(1,.28f,.06f),3);GameObject core=Disc("Impact core",at,impactSize*.5f,new Color(1,.78f,.25f),4);StartCoroutine(ShrinkCore(core,10f));EmitBurst(new Vector3(at.x,at.y,-.1f),new Color(1,.38f,.08f),18,3.2f);cameraShake=Mathf.Max(cameraShake,.13f);
        if(Vector2.Distance(player.position,at)<impactSize*.5f+.25f)TakeDamage(30f);yield return new WaitForSeconds(.28f);Destroy(hit);
    }
    IEnumerator ShrinkCore(GameObject core,float duration)
    {
        float elapsed=0f;float initialSize=core.transform.localScale.x;
        while(elapsed<duration)
        {
            if(!core)yield break;
            elapsed+=Time.deltaTime;
            float scale=Mathf.Lerp(initialSize,.3f,elapsed/duration);
            core.transform.localScale=Vector3.one*scale;
            yield return null;
        }
        if(core)Destroy(core);
    }
    IEnumerator TideLoop(){while(true){float intervalReduction=(stage-1)*.25f;yield return new WaitForSeconds(Mathf.Max(.65f,Random.Range(3f,4.2f)-intervalReduction));if(ended||changing||!IsWorldActive(World.Tidal))continue;StartCoroutine(Wave(Random.Range(-2.6f,2.6f),0));}}
    IEnumerator EarthquakeLoop(){while(true){yield return new WaitForSeconds(Random.Range(2.7f,3.8f));if(ended||changing||!IsWorldActive(World.Earthquake))continue;int fissuresPerQuake=Mathf.Min(1+(stage-1)/2,5);for(int i=0;i<fissuresPerQuake;i++)StartCoroutine(Fissure(0));}}
    IEnumerator AcidRainLoop(){while(true){yield return new WaitForSeconds(Random.Range(.06f,.11f));if(ended||changing||!IsWorldActive(World.AcidRain))continue;StartCoroutine(AcidDrop());}}
    IEnumerator AcidDrop()
    {
        float x=RandomWorldPosition(-7.8f,7.8f,0f,0f).x;float size=Random.Range(.11f,.2f);float speed=Random.Range(8f,12f)+(stage-1)*1.25f;
        GameObject drop=Rect("Acid droplet",new Vector3(x,Random.Range(5.4f,6.4f),-.25f),size*.65f,size,new Color(.55f,1f,.18f,.92f),3);
        drop.transform.SetParent(hazardsRoot,true);
        while(drop&&!ended&&!changing&&IsWorldActive(World.AcidRain)&&drop.transform.position.y> -5.6f)
        {
            Vector3 position=drop.transform.position;position.y-=speed*Time.deltaTime;drop.transform.position=position;
            if(Mathf.Abs(player.position.x-position.x)<.34f&&Mathf.Abs(player.position.y-position.y)<.38f){TakeDamage(5f);EmitBurst(position,new Color(.65f,1f,.12f),5,1.2f);Destroy(drop);yield break;}
            yield return null;
        }
        if(drop)Destroy(drop);
    }
    IEnumerator Fissure(int side)
    {
        List<Vector2> path=BuildFissurePath(side,Random.value>.5f);
        GameObject crack=new GameObject("Fissure warning");crack.transform.SetParent(hazardsRoot,false);
        for(int i=0;i<path.Count-1;i++)
        {
            Vector2 from=path[i],to=path[i+1],delta=to-from;Vector2 midpoint=(from+to)*.5f;
            GameObject segment=Rect("Fissure segment",new Vector3(midpoint.x,midpoint.y,-.2f),delta.magnitude,.24f,new Color(1,.35f,.08f,.82f),2);
            segment.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);segment.transform.SetParent(crack.transform,true);
        }
        yield return new WaitForSeconds(1.1f);
        if(!crack||ended||changing||!IsWorldActive(World.Earthquake)){if(crack)Destroy(crack);yield break;}
        crack.name="Ground fissure";foreach(SpriteRenderer segmentRenderer in crack.GetComponentsInChildren<SpriteRenderer>())segmentRenderer.color=new Color(.035f,.025f,.02f,1f);
        for(int i=1;i<path.Count;i+=3)EmitBurst(new Vector3(path[i].x,path[i].y,-.15f),new Color(.72f,.38f,.15f),3,.8f);
        FissureData data=new FissureData{path=path,side=side};fissures.Add(data);
        yield return new WaitForSeconds(2.4f);
        fissures.Remove(data);Destroy(crack);
    }
    List<Vector2> BuildFissurePath(int side,bool horizontal)
    {
        var path=new List<Vector2>();
        if(horizontal)
        {
            float minX=side<0?-7.8f:-7.8f,maxX=side>0?7.8f:side<0?-.2f:7.8f;
            if(side>0)minX=.2f;
            float centerY=Random.Range(-3.2f,3.2f);int segments=side==0?14:7;
            for(int i=0;i<=segments;i++)path.Add(new Vector2(Mathf.Lerp(minX,maxX,(float)i/segments),Mathf.Clamp(centerY+Random.Range(-.8f,.8f),-4.6f,4.6f)));
        }
        else
        {
            float centerX=side<0?Random.Range(-6.7f,-2.5f):side>0?Random.Range(2.5f,6.7f):Random.Range(-6.5f,6.5f);
            int segments=10;
            for(int i=0;i<=segments;i++)
            {
                float y=Mathf.Lerp(-4.7f,4.7f,(float)i/segments);float x=centerX+Random.Range(-.75f,.75f);
                x=side<0?Mathf.Clamp(x,-7.8f,-.2f):side>0?Mathf.Clamp(x,.2f,7.8f):Mathf.Clamp(x,-7.8f,7.8f);
                path.Add(new Vector2(x,y));
            }
        }
        return path;
    }
    void ApplyFissureContact()
    {
        if(!IsWorldActive(World.Earthquake)||falling)return;
        foreach(FissureData fissure in fissures)
        {
            if(fissure.triggered)continue;
            bool insideSide=fissure.side==0||(fissure.side<0?player.position.x<0f:player.position.x>=0f);bool overGap=false;
            if(insideSide)for(int i=0;i<fissure.path.Count-1;i++)if(Vector2.Distance(player.position,ClosestPointOnSegment(player.position,fissure.path[i],fissure.path[i+1]))<.47f){overGap=true;break;}
            if(!overGap)continue;
            fissure.triggered=true;falling=true;TakeDamage(20f);EmitBurst(new Vector3(player.position.x,player.position.y,-.1f),new Color(.78f,.46f,.2f),10,1.6f);cameraShake=Mathf.Max(cameraShake,.07f);StartCoroutine(FallAndRecover(fissure));break;
        }
    }
    static Vector2 ClosestPointOnSegment(Vector2 point,Vector2 start,Vector2 end)
    {
        Vector2 delta=end-start;float lengthSquared=delta.sqrMagnitude;if(lengthSquared<.0001f)return start;
        return start+delta*Mathf.Clamp01(Vector2.Dot(point-start,delta)/lengthSquared);
    }
    IEnumerator FallAndRecover(FissureData fissure)
    {
        Vector2 playerPosition=player.position,closest=fissure.path[0],tangent=Vector2.right;float closestDistance=float.MaxValue;
        for(int i=0;i<fissure.path.Count-1;i++)
        {
            Vector2 candidate=ClosestPointOnSegment(playerPosition,fissure.path[i],fissure.path[i+1]);float distance=(candidate-playerPosition).sqrMagnitude;
            if(distance<closestDistance){closestDistance=distance;closest=candidate;tangent=(fissure.path[i+1]-fissure.path[i]).normalized;}
        }
        Vector2 normal=new Vector2(-tangent.y,tangent.x);float escapeSide=Vector2.Dot(playerPosition-closest,normal)<0f?-1f:1f;
        Vector2 safe=closest+normal*escapeSide*.85f;Vector3 safePosition=new Vector3(Mathf.Clamp(safe.x,-7.65f,7.65f),Mathf.Clamp(safe.y,-4.65f,4.65f),0);
        float elapsed=0f;
        while(elapsed<.22f)
        {
            elapsed+=Time.deltaTime;float scale=Mathf.Lerp(.52f,.06f,elapsed/.22f);player.localScale=Vector3.one*scale;yield return null;
        }
        player.position=safePosition;player.localScale=Vector3.one*.52f;falling=false;
    }
    IEnumerator Wave(float y,int side)
    {
        float band=Random.Range(1.05f,1.55f);bool left=Random.value>.5f;
        float minX=side<0?-8f:side>0?0f:-8f,maxX=side<0?0f:side>0?8f:8f;
        float x=left?minX-2f:maxX+2f,end=left?maxX+2f:minX-2f;
        var wave=Rect("Tsunami",new Vector3(x,y,-.3f),2,band*2,new Color(.12f,.75f,.95f,.72f),3);
        wave.transform.SetParent(hazardsRoot,true);
        banner.color=new Color(.02f,.26f,.4f,.92f);banner.GetComponentInChildren<Text>().text=left?"WAVE INCOMING  →":"←  WAVE INCOMING";banner.gameObject.SetActive(true);EmitBurst(new Vector3(x,y,-.15f),new Color(.25f,.85f,1f),14,2.4f);yield return new WaitForSeconds(1.05f);
        if(!wave||ended||changing||!IsWorldActive(World.Tidal)){if(wave)Destroy(wave);yield break;}banner.gameObject.SetActive(false);
        bool hit=false;while(wave&&!ended&&!changing&&IsWorldActive(World.Tidal)&&(left?wave.transform.position.x<end:wave.transform.position.x>end))
        {
            float dt=Time.deltaTime;var p=wave.transform.position;p.x+=(left?16:-16)*dt;wave.transform.position=p;
            bool touching=Mathf.Abs(player.position.y-y)<band+.34f&&Mathf.Abs(player.position.x-p.x)<1.25f;
            if(touching&&!hit){hit=true;TakeDamage(40f);EmitBurst(new Vector3(player.position.x,player.position.y,-.1f),new Color(.25f,.85f,1f),12,2f);cameraShake=Mathf.Max(cameraShake,.1f);player.position=new Vector3(player.position.x,Mathf.Clamp(y+(player.position.y<y?-1:1)*(band+.7f),-4.6f,4.6f),0);}
            yield return null;
        }if(wave)Destroy(wave);
    }
    void ApplyCoreContactDamage(float dt)
    {
        for(int i=0;i<hazardsRoot.childCount;i++)
        {
            Transform hazard=hazardsRoot.GetChild(i);
            float contactRadius=(hazard.localScale.x+.52f)*.5f;
            if(hazard.name=="Impact core"&&Vector2.Distance(player.position,hazard.position)<contactRadius)TakeDamage(10f*dt);
        }
    }
    void CreateDarknessLights()
    {
        lightZones.Clear();int count=Mathf.Max(1,9-stage);float radius=1.65f;
        for(int i=0;i<count;i++)
        {
            Vector2 position=RandomWorldPosition(-7.1f,7.1f,-4.1f,4.1f);int attempts=0;
            while(attempts<12)
            {
                bool crowded=false;foreach(LightZone existing in lightZones)if(Vector2.Distance(position,existing.position)<2.8f){crowded=true;break;}
                if(!crowded)break;position=RandomWorldPosition(-7.1f,7.1f,-4.1f,4.1f);attempts++;
            }
            GameObject glow=Disc("Safe light",position,radius*2f,new Color(.8f,1f,.4f,.14f),2);
            GameObject source=Disc("Light source",position,.24f,new Color(1f,.96f,.68f,.95f),4);
            lightZones.Add(new LightZone{position=position,radius=radius,glow=glow,source=source});
        }
    }
    void ApplyDarknessDamage(float dt)
    {
        foreach(LightZone light in lightZones)if(Vector2.Distance(player.position,light.position)<light.radius)return;
        TakeDamage(5f*dt);
    }
    void UpdateDarknessMask()
    {
        for(int i=0;i<darknessPixels.Length;i++)darknessPixels[i]=new Color32(0,0,0,248);
        foreach(LightZone light in lightZones)StampDarknessOpening(light.position,light.radius);
        StampDarknessOpening(player.position,.55f);
        darknessTexture.SetPixels32(darknessPixels);darknessTexture.Apply(false,false);
    }
    void StampDarknessOpening(Vector2 position,float radius)
    {
        Vector3 viewport=cam.WorldToViewportPoint(new Vector3(position.x,position.y,0));
        if(viewport.z<=0f)return;
        float centerX=viewport.x*(DarknessMaskWidth-1),centerY=viewport.y*(DarknessMaskHeight-1);
        float radiusPixels=radius*DarknessMaskHeight/(cam.orthographicSize*2f);
        int minX=Mathf.Max(0,Mathf.FloorToInt(centerX-radiusPixels)),maxX=Mathf.Min(DarknessMaskWidth-1,Mathf.CeilToInt(centerX+radiusPixels));
        int minY=Mathf.Max(0,Mathf.FloorToInt(centerY-radiusPixels)),maxY=Mathf.Min(DarknessMaskHeight-1,Mathf.CeilToInt(centerY+radiusPixels));
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
        {
            float dx=(x-centerX)/radiusPixels,dy=(y-centerY)/radiusPixels,distance=Mathf.Sqrt(dx*dx+dy*dy);
            if(distance>1f)continue;
            byte alpha=(byte)Mathf.RoundToInt(248f*Mathf.SmoothStep(.76f,1f,distance));int index=y*DarknessMaskWidth+x;
            if(alpha<darknessPixels[index].a)darknessPixels[index]=new Color32(0,0,0,alpha);
        }
    }
    void TakeDamage(float amount){if(ended)return;health=Mathf.Max(0,health-amount);flashTime=.2f;damageTintTime=.18f;if(health<=0){ended=true;messageText.text="LOST BETWEEN DIMENSIONS  •  PRESS R TO RESTART";banner.GetComponentInChildren<Text>().text="DIMENSION LOST";banner.gameObject.SetActive(true);}}
    void ClearHazards(){for(int i=hazardsRoot.childCount-1;i>=0;i--)Destroy(hazardsRoot.GetChild(i).gameObject);fissures.Clear();lightZones.Clear();if(darknessOverlay)darknessOverlay.gameObject.SetActive(false);}
    void Restart(){StopAllCoroutines();ClearHazards();health=100f;survived=0;stage=1;ended=false;changing=false;falling=false;overlapMode=false;flashTime=0;damageTintTime=0;cameraShake=0;cam.transform.position=new Vector3(0,0,-10);playerRenderer.color=playerColor;player.localScale=Vector3.one*.52f;player.position=Vector3.zero;world=(World)Random.Range(0,5);secondaryWorld=world;SetWorld(world,true);StartCoroutine(PhaseLoop());StartCoroutine(VolcanoLoop());StartCoroutine(TideLoop());StartCoroutine(EarthquakeLoop());StartCoroutine(AcidRainLoop());}
    GameObject Rect(string n,Vector3 p,float w,float h,Color c,int order){var o=new GameObject(n);o.transform.position=p;o.transform.localScale=new Vector3(w,h,1);var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Square;r.color=c;r.sortingOrder=order;return o;}
    GameObject Disc(string n,Vector2 p,float s,Color c,int order){var o=new GameObject(n);o.transform.SetParent(hazardsRoot,false);o.transform.position=p;o.transform.localScale=Vector3.one*s;var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Circle;r.color=c;r.sortingOrder=order;return o;}
    static Sprite CreateCrackSprite()
    {
        const int width=512,height=288;var pixels=new Color[width*height];var random=new System.Random(59);
        Vector2 center=new Vector2(width*.5f,height*.5f);
        for(int i=0;i<18;i++)
        {
            float angle=Mathf.PI*2*i/18f+(float)(random.NextDouble()-.5)*.12f;
            float widthAngle=(float)(.16+random.NextDouble()*.2),tipAngle=angle+widthAngle*.5f+(float)(random.NextDouble()-.5)*.16f;
            Vector2 innerA=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*random.Next(38,58);
            Vector2 innerB=center+new Vector2(Mathf.Cos(angle+widthAngle),Mathf.Sin(angle+widthAngle))*random.Next(38,58);
            Vector2 tip=center+new Vector2(Mathf.Cos(tipAngle),Mathf.Sin(tipAngle))*random.Next(115,255);
            Color glass=new Color(.62f,.88f,1f,(float)(.18+random.NextDouble()*.24));
            FillTriangle(pixels,width,height,innerA,innerB,tip,glass);
            DrawCrackLine(pixels,width,height,innerA,tip,1);DrawCrackLine(pixels,width,height,innerB,tip,1);
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false); texture.filterMode=FilterMode.Bilinear; texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100f);
    }
    static void FillTriangle(Color[] pixels,int width,int height,Vector2 a,Vector2 b,Vector2 c,Color color)
    {
        int minX=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))));
        int maxX=Mathf.Min(width-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
        int minY=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
        int maxY=Mathf.Min(height-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
        float area=Cross(b-a,c-a);
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
        {
            Vector2 p=new Vector2(x+.5f,y+.5f);
            float e1=Cross(b-a,p-a),e2=Cross(c-b,p-b),e3=Cross(a-c,p-c);
            if((e1>=0f&&e2>=0f&&e3>=0f)||(e1<=0f&&e2<=0f&&e3<=0f))pixels[y*width+x]=color;
        }
    }
    static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    static void DrawCrackLine(Color[] pixels,int width,int height,Vector2 from,Vector2 to,int radius)
    {
        int x0=Mathf.RoundToInt(from.x),y0=Mathf.RoundToInt(from.y),x1=Mathf.RoundToInt(to.x),y1=Mathf.RoundToInt(to.y);
        int dx=Mathf.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Mathf.Abs(y1-y0),sy=y0<y1?1:-1,error=dx+dy;
        while(true)
        {
            for(int oy=-radius;oy<=radius;oy++)for(int ox=-radius;ox<=radius;ox++)
            {
                int x=x0+ox,y=y0+oy;if(x>=0&&x<width&&y>=0&&y<height)pixels[y*width+x]=Color.white;
            }
            if(x0==x1&&y0==y1)break;int twice=2*error;
            if(twice>=dy){error+=dy;x0+=sx;}if(twice<=dx){error+=dx;y0+=sy;}
        }
    }
    static Text Label(Transform root,string value,int size,TextAnchor align,Vector2 amin,Vector2 amax,Vector2 pos,Vector2 dim,Color color){var o=new GameObject("Label");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=amin;r.anchorMax=amax;r.pivot=new Vector2(amin.x==amax.x?amin.x:.5f,amin.y==amax.y?amin.y:.5f);r.anchoredPosition=pos;r.sizeDelta=dim;var t=o.AddComponent<Text>();t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=align;t.color=color;t.horizontalOverflow=HorizontalWrapMode.Overflow;return t;}
    static Image Panel(Transform root,Vector2 anchor,Vector2 pos,Vector2 size,Color color){var o=new GameObject("Panel");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;var i=o.AddComponent<Image>();i.color=color;return i;}
    static class Sprites{static Sprite square,circle;public static Sprite Square=>square??(square=Make(false));public static Sprite Circle=>circle??(circle=Make(true));static Sprite Make(bool round){int n=round?64:2;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1;pixels[y*n+x]=!round||dx*dx+dy*dy<=1?Color.white:Color.clear;}t.SetPixels(pixels);t.Apply();return Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);}}
}

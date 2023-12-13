using Huya.USDK.MediaController;
using System;
using UnityEngine;
using UnityEngine.UI;
using LitJson;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;

public class NewBehaviourScript : MonoBehaviour
{
    public Camera mainCamera;
    public Image imageClock;
    public Button startSceneLayoutBtn;
    public Button stopSceneLayoutBtn;
    public Button pkInviteOnBtn;
    public Button pkInviteOffBtn;
    public Button normalBusBtn;
    public Button normalAppletBusBtn;
    public Text rspText;

    private Huya.USDK.MediaController.MediaPiplelineControllerSDK mediaControllerSDK; // ý�����SDK
    private int reqIdIndex = 0;
    private int resWidth = 800; // ����ҵ���ȷ����ϷͶ���ֱ��ʴ�С
    private int resHeight = 600;
    private float ratio = -20f;
    private float timer = 0.033f; // ��ʱ0.033f�룬ҵ�����Զ���
    private Texture2D gameTexture;
    private RenderTexture renderTexture;

    void Awake()
    {
        Init();
    }

    // Start is called before the first frame update
    void Start()
    {
        startSceneLayoutBtn.onClick.AddListener(OnStartSceneLayoutReqMsg);
        stopSceneLayoutBtn.onClick.AddListener(OnStopSceneLayoutReqMsg);
        pkInviteOnBtn.onClick.AddListener(OnPkInviteOnMessageListenMsg);
        pkInviteOffBtn.onClick.AddListener(OnPkInviteOffMessageListenMsg);
        normalBusBtn.onClick.AddListener(OnGetAnchorLiveStatus);
        normalAppletBusBtn.onClick.AddListener(OnGetLiveInfo);

        // ģ����Է��ͻ�ȡ�����˻�����Ϣ
        InvokeRepeating("TestGetAnchorCanvasMsg", 3f, 5f);
    }

    private void Init()
    {
        mediaControllerSDK = new Huya.USDK.MediaController.MediaPiplelineControllerSDK();
        mediaControllerSDK.onStreamCustomData = OnStreamCustomData;
        mediaControllerSDK.onStreamLog = OnStreamLog;
        mediaControllerSDK.onAppletInvokeRspMsg = OnDealAppletInvokeRspMsg;
        mediaControllerSDK.onAppletInvokeListenRspMsg = OnDealAppletInvokeListenMsg;
        mediaControllerSDK.onAppletMsg = OnDealAppletMsg;
        mediaControllerSDK.onAnchorMsg = OnDealAnchorMsg;

        mediaControllerSDK.SetOutFPS(30);
        mediaControllerSDK.Init();
    }

    // Update is called once per frame
    void Update()
    {
        // ʱ����ת
        TimeSpan timeSpan = DateTime.Now.TimeOfDay;
        imageClock.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * (float)timeSpan.TotalSeconds + 90f);

        mediaControllerSDK.Update();

        // ���Ƹ��·�����Ƶ����Ƶ��
        // ע��������Ҫ�ر��Ƶ���Ϸ��Ƶ���棬��ɲ��õ���SendVideoData�����������Զ��ɼ���Ϸ����
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            //SendVideoData();
            timer = 0.033f;
        }
    }

    private void SendVideoData()
    {
        // ҵ���ɸ����Լ����������п�����Ƶ����ķֱ��ʣ��˴���ȡ����ͷ���ػ����С
        resWidth = mainCamera.pixelWidth;
        resHeight = mainCamera.pixelHeight;

        // TODO����Ϸ���������Ƿ�ת�ģ�ҵ����������shader���Կ������·�ת
        if (renderTexture == null)
        {
            RenderTextureDescriptor renderTextureDescriptor =
                new RenderTextureDescriptor(resWidth, resHeight, RenderTextureFormat.BGRA32);
            renderTextureDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            renderTexture = new RenderTexture(renderTextureDescriptor);
        }

        if (gameTexture == null)
        {
            gameTexture = new Texture2D(resWidth, resHeight, TextureFormat.RGBA32, false);
        }

        mainCamera.targetTexture = renderTexture;
        mainCamera.Render();

        RenderTexture.active = renderTexture;
        //gameTexture.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        //gameTexture.Apply();
        Graphics.ConvertTexture(renderTexture, gameTexture);
        mainCamera.targetTexture = null;
        RenderTexture.active = null;

        // ����Ҫ�����Ƶ���棬���Ե���SendVideo������ĵ���Ľӿڵ���2.1.2 UpdateAnchorLayer
        // mediaControllerSDK.SendVideo("some stream name", gameTexture.GetNativeTexturePtr(), DateTime.Now.Millisecond);

        // ��ֻ��Ҫһ����Ƶ���ƻ��棬���Ե���SendOneVideo
        mediaControllerSDK.SendOneVideo(gameTexture.GetNativeTexturePtr(), DateTime.Now.Millisecond);

        // ��ֻ��Ҫһ����Ƶ������Ϊ��Ϸ���棬��ɲ��õ���SendVideo��ؽӿ�,
        // �������ѿ��Զ��ɼ�, ��ҵ������Լ�Ͷ����Ϸ���棬��ǰ��С�������hyExt.exe.launchGameʱoptParam��manualCastScreenΪtrue���������������ͬͼ�㻭��
    }

    public void OnStreamCustomData(CustomDataEvent customDataEvent)
    {
        // ҵ���ɸ���customDataEvent.customData�����ݣ���������Ӧ������
        // ���������˵Ļ�����С��ҵ�����øû�����С����͸��ͼ���СҲ���Զ����С
        if(customDataEvent.customData == null)
        {
            return;
        }

        JsonData jsonRootData = JsonMapper.ToObject(customDataEvent.customData);
        if(!jsonRootData.ContainsKey("custom_data"))
        {
            return;
        }

        var customData = jsonRootData["custom_data"];
        if (!customData.ContainsKey("out_stream"))
        {
            return;
        }

        var outStream = customData["out_stream"];
        if (outStream.ContainsKey("width"))
        {
            resWidth = (int)outStream["width"];
        }

        if (outStream.ContainsKey("height"))
        {
            resHeight = (int)outStream["height"];
        }
    }

    public void OnStreamLog(string message, LogLevel level)
    {
        Debug.Log(message);
    }

    // ��ȡ�����˿���״̬��������Ϣ
    public void OnGetAnchorLiveStatus()
    {
        var eventName = "GetAnchorStatus";
        var reqId = eventName + "_" + reqIdIndex.ToString();
        JsonData messageJsonData = new JsonData();
        messageJsonData["typeName"]  ="Live";
        mediaControllerSDK.SendGeneralMsg(eventName, reqId, messageJsonData);
        reqIdIndex++;
    }

    // ͨ��С�����ȡ����ֱ����Ϣ
    public void OnGetLiveInfo()
    {
        var appletApiName = "hyExt.context.getLiveInfo";
        var reqId = appletApiName + "_" + reqIdIndex.ToString();

        JsonData appletApiParamjsonData = new JsonData();
        appletApiParamjsonData.SetJsonType(JsonType.Object); // ��json����ʱ(�ղ�)���������Ǳ�Ҫ��
        var appletApiParamStr = appletApiParamjsonData.ToJson();

        List<string> appletApiParamArray = new List<string>();
        appletApiParamArray.Add(appletApiParamStr);

        mediaControllerSDK.SendAppletInvokeReqMsg(appletApiName, appletApiParamArray, reqId);
    }

    // ģ�ⷢ�ͻ�ȡ�����˻�����Ϣ
    public void TestGetAnchorCanvasMsg()
    {
        var eventName = "GetAnchorCanvas";
        var reqId = eventName + "_" + reqIdIndex.ToString();
        JsonData messageJsonData = new JsonData();
        messageJsonData.SetJsonType(JsonType.Object); // ��json����ʱ(�ղ�)���������Ǳ�Ҫ��
        mediaControllerSDK.SendGeneralMsg(eventName, reqId, messageJsonData);
        reqIdIndex++;
    }

    // ����С������ÿ����������ֵ�������Ϣ
    public void OnStartSceneLayoutReqMsg()
    {
        var appletApiName = "hyExt.stream.startSceneLayout";
        var reqId = appletApiName + "_" + reqIdIndex.ToString();

        JsonData appletApiParamjsonData = new JsonData();
        appletApiParamjsonData["scene"] = "onePkMode";
        var appletApiParamStr = appletApiParamjsonData.ToJson();

        List<string> appletApiParamArray = new List<string>();
        appletApiParamArray.Add(appletApiParamStr);

        mediaControllerSDK.SendAppletInvokeReqMsg(appletApiName, appletApiParamArray, reqId);
    }

    // ����С�������ֹͣ�������ֵ�������Ϣ
    public void OnStopSceneLayoutReqMsg()
    {
        var appletApiName = "hyExt.stream.stopSceneLayout";
        var reqId = appletApiName + "_" + reqIdIndex.ToString();
        List<string> appletApiParamArray = new List<string>();
        mediaControllerSDK.SendAppletInvokeReqMsg(appletApiName, appletApiParamArray, reqId);
    }

    // ����С���������ӿڵ�������Ϣ
    private void OnPkInviteOnMessageListenMsg()
    {
        // ����С����ǰ��JS�ӿڣ�hyExt.pk.onInviteMessage
        var appletApiName = "hyExt.pk.onInviteMessage";
        var reqId = appletApiName + "_" + reqIdIndex.ToString();

        JsonData appletApiParamjsonData = new JsonData();

        // ��appletApiCallbackId��ΪӦ�����cbId
        var appletApiCallbackId = "HYExeCallback_" + reqIdIndex.ToString();
        appletApiParamjsonData["callback"] = appletApiCallbackId;
        var appletApiParamStr = appletApiParamjsonData.ToJson();

        List<string> appletApiParamArray = new List<string>();
        appletApiParamArray.Add(appletApiParamStr);

        var listenOnOff = "on";             
        var eventType = "InviteMessage"; // �¼����ͣ���ѡ
        mediaControllerSDK.SendAppletInvokeListenMsg(appletApiName, appletApiParamArray, reqId, listenOnOff, eventType);
    }

    // ����С����ȡ��������ӿڵ�������Ϣ
    private void OnPkInviteOffMessageListenMsg()
    {
        // ����С����ǰ��JS�ӿڣ�hyExt.pk.offInviteMessage
        var appletApiName = "hyExt.pk.offInviteMessage";
        var reqId = appletApiName + "_" + reqIdIndex.ToString();
        List<string> appletApiParamArray = new List<string>();
        var listenOnOff = "off";
        var eventType = "InviteMessage"; // �¼����ͣ���ѡ
        mediaControllerSDK.SendAppletInvokeListenMsg(appletApiName, appletApiParamArray, reqId, listenOnOff, eventType);
    }


    // С��������invokeӦ��ͨ����Ϣ
    private void OnDealAppletInvokeRspMsg(string apiName, string rsp, string reqId, string err)
    {
        // TODO: ҵ������д���С����invoke����Ӧ��ͨ��
        if(apiName == "hyExt.stream.startSceneLayout")
        {
            Debug.Log("start scene layout result rsp: " + rsp + ", reqId: " + reqId);

            if(err == "")
            {
                rspText.text = "start scene layout success, rsp msg: " + rsp;
            }
            else
            {
                rspText.text = "start scene layout failed, error: " + err;
            }
        }
        else if (apiName == "hyExt.stream.stopSceneLayout")
        {
            Debug.Log("stop scene layout result rsp: " + rsp + ", reqId: " + reqId);

            if (err == "")
            {
                rspText.text = "stop scene layout success, rsp msg: " + rsp;
            }
            else
            {
                rspText.text = "stop scene layout failed, error: " + err;
            }
        }
        else if (apiName == "hyExt.pk.onInviteMessage")
        {
            Debug.Log("pk invite listen on rsp: " + rsp + ", reqId: " + reqId);

            if (err == "")
            {
                rspText.text = "pk invite listen on success, rsp msg: " + rsp;
            }
            else
            {
                rspText.text = "pk invite listen on failed, rsp error: " + err;
            }
        }
        else if (apiName == "hyExt.pk.offInviteMessage")
        {
            Debug.Log("pk invite listen off rsp: " + rsp + ", reqId: " + reqId);

            if (err == "")
            {
                rspText.text = "pk invite listen off success, rsp msg: " + rsp;
            }
            else
            {
                rspText.text = "pk invite listen off failed, rsp error: " + err;
            }
        }
        else if(apiName == "hyExt.context.getLiveInfo")
        {
            Debug.Log("get live info rsp: " + rsp + ", reqId: " + reqId);
            rspText.text = "get live info rsp msg: " + rsp;
        }
        else // ����invoke����Ӧ��
        {

        }
    }

    // С��������invoke listen ����ͨ����Ϣ
    private void OnDealAppletInvokeListenMsg(string apiName, string message, string cbId)
    {
        // TODO: ҵ������д���С����invoke listen ����ͨ��
        if (apiName == "hyExt.pk.onInviteMessage")
        {
            Debug.Log("get pk invite message: " + message + ", cbId: " + cbId);
            rspText.text = "pk invite msg: " + message;

            // messageתjson��ȡPkNotice���ֶ�ֵ����
            // �ο��ĵ���https://dev.huya.com/docs/miniapp/danmugame/open/1v1/hy-ext-pk-on-invite-message/
            JsonData messageJsonData = JsonMapper.ToObject(message);
            if (messageJsonData.ContainsKey("name") && messageJsonData.ContainsKey("message"))
            {
                // TODO: ҵ������д���С��������pk������Ϣ

            }
        }
        else if (apiName == "hyExt.pk.offInviteMessage")
        {
            Debug.Log("get pk invite off message: " + message + ", cbId: " + cbId);
            rspText.text = "pk invite msg: " + message;
        }
        else // ����invoke����ͨ��
        {

        }
    }

    // С����ǰ�˷�������Ϣ
    private void OnDealAppletMsg(string message)
    {
        // TODO: ҵ��ദ��С������������������Ϣ(��ҵ����Զ���ṹ������)
        // ��ҵ��಻����С����ǰ�˴��뿪�����ɺ��Դ���Ϣ����ʵ��
    }

    // �����˷�������Ϣ
    private void OnDealAnchorMsg(string eventName, JsonData messageJsonData)
    {
        if (eventName == "GetAnchorStatus_Callback") // ��ӦGetAnchorLiveStatus����Ӧ��
        {
            if (messageJsonData.ContainsKey("state"))
            {
                var state = (string)messageJsonData["state"];
                Debug.Log("get GetAnchorStatus msg, state: " + state);

                rspText.text = "anchor state: " + state;
            }
        }
        else if (eventName == "GetAnchorCanvas_Callback") // ��ӦTestGetAnchorCanvasMsg����Ӧ��
        {
            var layoutType = 0;
            var width = 0;
            var height = 0;
            if (messageJsonData.ContainsKey("layoutType"))
            {
                layoutType = (int)messageJsonData["layoutType"];
            }

            if (messageJsonData.ContainsKey("width"))
            {
                width = (int)messageJsonData["width"];
            }

            if (messageJsonData.ContainsKey("height"))
            {
                height = (int)messageJsonData["height"];
            }

            Debug.Log("get GetAnchorCanvas msg: " + layoutType + ", width: " + width + ", height: " + height);

            // TODO: ģ�ⷴ����תUI����(�����õ�)
            ratio = -ratio;
        }
        else // TODO: ������Ϣ��ҵ���ɸ����ĵ�˵����ҵ����Ҫ�����ж�Ӧ��ӽ�������Ӧ��
        {
            Debug.Log("get applet others channel msg: " + eventName);
        }
    }
}

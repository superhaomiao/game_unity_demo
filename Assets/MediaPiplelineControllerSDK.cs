using AOT;
using LitJson;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

// MediaPipelineControllerSDK �� Unity ��װ��
// ��SDK������:
// 1. �����������ˡ�ǰ��С������ͨ�š��ӿڵ���
// 2. ����Ͷ���������˿��Զ������ͼ��Ͷ��
// ע��
//   ��ҵ��಻�������Ϸͼ���ֻͶ����ǰ��Ϸ���汾����ɺ��Ե���UpdateAnchorLayer��cpp_pipeline_add_*��ϵ�нӿڣ������˻��Զ��ɼ���Ϸ����)
//   ҵ���ɸ���������Ҫʹ��������������Ҳ��ֻ������һ�֣����磺����ǰ��С����������˼��ͨ�Ż��ѯ�ӿ�
//   ������Ϸ���浽������ʱ���Ƽ����Ȳ�������ʽ����������Ҫ������Ϸ��Ƶ������ֱ�ӱ��ز��ż��ɣ������˻�ɼ�
// �ⲿ��ϷGame��С����һ�廯�����ӿ��ĵ���https://github.com/weigod/game_sdk_demo/blob/master/game_interface.md
// С����ӿ�SDK�ĵ���https://dev.huya.com/docs/miniapp/dev/sdk/
// C++��C#��װ�ӿڽ����ĵ���https://github.com/weigod/game_sdk_demo

namespace Huya.USDK.MediaController
{
    // sdk ���ɵ��Զ��������¼�
    public struct CustomDataEvent
    {
        public ulong uid;
        public string streamUrl;
        public string streamUUID;
        public string streamUUIDS;
        public string customData; // json �ַ���(��Ҫ)
    }

    // ԭʼ��Ƶ����֡(��)
    public struct RawVideoFrame
    {
        public int frameID;
        public int width;
        public int height;
        public int lineSize;
        public int pixelFormat;
        public IntPtr pixelData;
        public int fps;
    }

    // ��Ƶ����֡��Ϣ(��)
    public struct PCMInfo
    {
        public int sampleRate;
        public int channelLayout;
        public int sampleCount;
        public int audioFormat;
    }

    // ��Ƶ֡
    public class VideoFrame
    {
        public string streamUUID;       // ����ַ����
        public IntPtr nativeTex;        // ��Ϸ����ԭ����������ʹ�á���Чʱ��ʹ�� data
        public int width;               // ��Ϸ������
        public int height;              // ��Ϸ����߶�
        public TextureFormat format;    // ֧�ֵ���Чֵ: TextureFormat.RGBA32, TextureFormat.BGRA32
        public byte[] data;             // ��Ϸ��������
        public long pts;                // pts
        public byte[] customData;       // �Զ�������
        public int customDataSize;      // �Զ����������ݴ�С, ��ʹ�� customData.Length ��ȡ��С
    }

    // ��Ƶ֡
    public class AudioFrame
    {
        public int sampleRate;          // ������
        public int sampleCount;         // ��������
        public byte[] data;             // ��Ƶ����
        public int dataSize;            // ��Ƶ�������ݴ�С, ��ʹ�� data.Length ��ȡ��С
        public long pts;                // pts
        public byte[] customData;       // �Զ�������
    }

    // ��־����
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    // �������ַ�����б�(��Ҫ������������Ϸ����ʱ�õ�,һ������һ������һ�����ֱ���Ƶ������Ƶ��)
    [Serializable]
    public class OutputStreams
    {
        public List<String> cpp_stream_uuids;
    }

    /// ��װSDK��װ��ʵ��
    public class MediaPiplelineControllerSDK
    {
        /// �Զ��������ݻص�ί�С��᷵��ĳЩ���ã��������˷ֱ��ʵȣ�ҵ���ɸ�����Ҫ��ȡ��
        /// <param name="customDataEvent">�����Զ�������</param>
        public delegate void OnStreamCustomData(CustomDataEvent customDataEvent);

        /// ��־ί�У���ҵ�������Ҫ���ɳ�ʼ���ö�����дҵ����־
        /// <param name="message"></param>
        /// <param name="level"></param>
        public delegate void OnStreamLog(string message, LogLevel level);

        /// С����Invoke ����Ϸ�����Ϣͨ��ί��
        /// <param name="apiName"></param>
        /// <param name="rsp"></param>
        /// <param name="reqId"></param>
        /// <param name="err"></param>
        public delegate void OnAppletInvokeRspMsg(string apiName, string rsp, string reqId, string err);

        /// С����Invoke Listen ����Ϸ�����Ϣͨ��ί��
        /// <param name="apiName"></param>
        /// <param name="message"></param>
        /// <param name="cbId"></param>
        public delegate void OnAppletInvokeListenRspMsg(string apiName, string message, string cbId);

        /// С��������Ϸ���ͨ����Ϣͨ��ί��
        /// <param name="message"></param>
        public delegate void OnAppletMsg(string message);

        /// ����������Ϸ���ͨ����Ϣͨ��ί��
        /// <param name="eventName"></param>
        /// <param name="messageJsonData"></param>
        public delegate void OnAnchorMsg(string eventName, JsonData messageJsonData);

        /// �Զ��������ݻص�ί�С��᷵��ĳЩ���ã��������˷ֱ��ʵȣ�ҵ���ɸ�����Ҫ��ȡ��
        /// <param name="userData"></param>
        /// <param name="pipelineId"></param>
        /// <param name="dataEvent"></param>
        private delegate void CustomDataCallback(IntPtr userData, string pipelineId, CustomDataEvent dataEvent);
        private CustomDataCallback customDataCallback;

        /// ͨ��������Ϣ�ص�ί��
        /// <param name="userData"></param>
        /// <param name="texturePtr"></param>
        /// <param name="videoData"></param>
        private delegate void CppCallChannelMsgCallBack(IntPtr userData, string msg, uint len);
        private CppCallChannelMsgCallBack cppCallChannelMsgCallBack;

        // ����Ϊ�õ�����ҪCPP SDK�����ӿ�
        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_context_init3(int thriftPort, string jobId, string logDir, int thriftListenPort, bool enableDumpCapture);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern void cpp_context_uninit();

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_set_custom_data_cb(IntPtr cb, IntPtr userData);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_dump_sharedtexture(IntPtr cb, IntPtr userData, long sharedHandle, IntPtr context, IntPtr contextTexture);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_add_raw(string streamUUID, RawVideoFrame frame, IntPtr captureStartTickMs, ref long pts);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_add_texture(string streamUUID, IntPtr context, IntPtr texturePtr, int textureType, ref long pts, ref long fps);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_add_texture_jce(string streamUUID, IntPtr context, IntPtr texturePtr, int textureType, ref long pts, ref long fps, IntPtr customData, uint customDataSize);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_add_pcm(string streamUUID, PCMInfo pcm, IntPtr strData, uint strDataSize, ref long pts);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_pipeline_add_pcm_jce(string streamUUID, PCMInfo pcm, IntPtr strData, uint strDataSize, ref long pts, IntPtr customData, uint customDataSize);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_call_channel_msg(IntPtr sendBuf, uint sendBufLen);

        [DllImport("MediaPipelineControllerSDK.dll")]
        private static extern int cpp_set_channel_msg_cb(IntPtr cb, IntPtr userData);

        // �ⲿ�ص�������Ա
        public OnStreamCustomData onStreamCustomData;
        public OnStreamLog onStreamLog;
        public OnAppletInvokeRspMsg onAppletInvokeRspMsg;
        public OnAppletInvokeListenRspMsg onAppletInvokeListenRspMsg;
        public OnAppletMsg onAppletMsg;
        public OnAnchorMsg onAnchorMsg;

        // �ڲ���Ա
        private int mOutVideoFrameID = 0;
        private int mOutVideoFPS = 0;
        private GCHandle mHandleForThis;
        private List<string> mOutputStreams;
        private Dictionary<string, string> mMapFromInputStreamToOutputStream;
        private Queue<Action> mCustomQueue;
        private Queue<Action> mChannelMsgQueue;

        /// ��ʼ��SDK
        public int Init()
        {
            // ��ȡ��־·������ʼ��sdk��Ҫ��ҵ��Ҳ����ʹ�ø�·����������ҵ����־����·���±���ͳһ����
            var logDirEnv = GetLogDirPath();

            var port = Environment.GetEnvironmentVariable("CPP_PORT");
            if (port != null)
            {
                var jobId = Environment.GetEnvironmentVariable("CPP_JOB_ID");
                var listenPort = Environment.GetEnvironmentVariable("CPP_LISTEN_PORT"); // ����IDE����

                // ��ȡ������ַ
                var streamUUIDs = Environment.GetEnvironmentVariable("CPP_STREAM_UUIDS");
                if (streamUUIDs == null)
                {
                    onStreamLog?.Invoke("there is no env variable named CPP_STREAM_UUIDS", LogLevel.Error);
                    return -1;
                }

                var bytes = Convert.FromBase64String(streamUUIDs);
                var jsonString = Encoding.UTF8.GetString(bytes);
                var streams = JsonUtility.FromJson<OutputStreams>(jsonString);

                int thriftListenPort = 0;
                if (!string.IsNullOrEmpty(listenPort))
                {
                    thriftListenPort = int.Parse(listenPort);
                }
                else
                {
                    onStreamLog?.Invoke($"MediaPipelineControllerSDK thriftListenPort is null ", LogLevel.Warning);
                }
                return InitImpl(Int32.Parse(port), thriftListenPort, jobId, logDirEnv, streams.cpp_stream_uuids);
            }
            return InitImpl(0, 0, "", logDirEnv, new List<string>());
        }

        /// ��ʼ��ʵ��
        /// <param name="thriftPort">���� 0 ������ֵ�����ز���ʱ�����õ�����ʽ�����´ӻ���������ȡ��</param>
        /// <param name="thriftListenPort">���� 0 ������ֵ�����ز���ʱ�����õ�����ʽ�����´ӻ���������ȡ��</param>
        /// <param name="jobId">����ǿ��ַ��������ز���ʱ�����õ�����ʽ�����´ӻ���������ȡ��</param>
        /// <param name="logDir">��־·����null ʱ�� dll ͬ��Ŀ¼�����־��</param>
        /// <param name="oStreams">���������</param>
        private int InitImpl(int thriftPort, int thriftListenPort, string jobId, string logDir, List<string> oStreams)
        {
            onStreamLog?.Invoke($"MediaPipelineControllerSDK Init, thriftPort={thriftPort},thriftListenPort={thriftListenPort}, jobId={jobId}, logDir={logDir}", LogLevel.Info);
            mCustomQueue = new Queue<Action>();
            mChannelMsgQueue = new Queue<Action>();
            mOutputStreams = oStreams;
            mMapFromInputStreamToOutputStream = new Dictionary<string, string>();
            mHandleForThis = GCHandle.Alloc(this);
            mOutVideoFrameID = 0;
            int ret = 0;
            do
            {
                ret = cpp_context_init3(thriftPort, jobId, logDir, thriftListenPort, false);
                if (ret != 0)
                {
                    onStreamLog?.Invoke($"cpp_context_init3 error, {ret}", LogLevel.Error);
                    break;
                }

                customDataCallback = new CustomDataCallback(OnCustomDataImpl);
                ret = cpp_set_custom_data_cb(Marshal.GetFunctionPointerForDelegate(customDataCallback), GCHandle.ToIntPtr(mHandleForThis));
                if (ret != 0)
                {
                    onStreamLog?.Invoke($"cpp_set_custom_data_cb error, {ret}", LogLevel.Error);
                    break;
                }

                cppCallChannelMsgCallBack = new CppCallChannelMsgCallBack(OnCppCallChannelMsgCallBack);
                ret = cpp_set_channel_msg_cb(Marshal.GetFunctionPointerForDelegate(cppCallChannelMsgCallBack), GCHandle.ToIntPtr(mHandleForThis));
                if (ret != 0)
                {
                    onStreamLog?.Invoke($"cpp_set_channel_msg_cb error, {ret}", LogLevel.Error);
                    break;
                }

            } while (false);
            onStreamLog?.Invoke($"MediaPipelineControllerSDK Init finished, {ret}", LogLevel.Info);
            return ret;
        }

        /// ����ʼ��SDK
        public void Uninit()
        {
            cpp_context_uninit();
            mHandleForThis.Free();
        }

        // ����ͨ����Ϣ��ҵ�����Ҫ��Update�е��û�ĳ���߳��ж�ʱ����
        public void Update()
        {
            lock (mCustomQueue)
            {
                while (mCustomQueue.Count > 0)
                {
                    mCustomQueue.Dequeue().Invoke();
                }
            }

            lock (mChannelMsgQueue)
            {
                while (mChannelMsgQueue.Count > 0)
                {
                    mChannelMsgQueue.Dequeue().Invoke();
                }
            }
        }

        /// <summary>
        /// �������õ��������ַ�б�
        /// Init ֮����á�
        /// </summary>
        public List<string> OutputStreams
        {
            get => mOutputStreams;
            set => mOutputStreams = value;
        }

        public void AddOutputStream(string streams)
        {
            if (mOutputStreams != null)
            {
                mOutputStreams.Add(streams);
            }
        }

        public void RemoveOutputStream(string streams)
        {
            if (mOutputStreams != null)
            {
                if (mOutputStreams.Count > 0)
                {
                    mOutputStreams.Remove(streams);
                }
            }
        }

        /// ָ������ FPS
        /// <param name="fps"></param>
        public void SetOutFPS(int fps)
        {
            mOutVideoFPS = fps;
        }

        /// ����Ƶ��
        /// �����ڴ��� sdk �����ͷš�
        /// <param name="srcStreamUUID">Ŀ������ַ����Ҫ���͵� frame �Ǵ������������������</param>
        /// <param name="frame"></param>
        public int SendVideo(string srcStreamUUID, VideoFrame frame)
        {
            var outputStream = GetOutputStreamByInputStream(srcStreamUUID);
            return SendVideoTo(outputStream, frame);
        }

        /// ����Ƶ������Ƶ��Ϊ�������ݡ�����һ�������ʱʹ�á�
        /// <param name="frame"></param>
        public int SendOneVideo(VideoFrame frame)
        {
            if (mOutputStreams.Count == 0)
            {
                onStreamLog?.Invoke("There is no output stream, please check your configuration.", LogLevel.Error);
                return -1;
            }
            if (mOutputStreams.Count > 1)
            {
                onStreamLog?.Invoke($"There are more than one output streams in configuration but you don't specify one, use the first output stream.", LogLevel.Warning);
            }
            return SendVideoTo(mOutputStreams[0], frame);
        }

        /// ����Ƶ���Ƶ�ָ������ַ��
        /// <param name="dstStreamUUID">Ŀ������ַ����������� OutputStreams �б��С�</param>
        /// <param name="frame"></param>
        public int SendVideoTo(string dstStreamUUID, VideoFrame frame)
        {
            RawVideoFrame rawFrame = new RawVideoFrame();
            rawFrame.frameID = ++mOutVideoFrameID;
            rawFrame.width = frame.width;
            rawFrame.height = frame.height;
            rawFrame.lineSize = frame.data.Length / frame.height;
            rawFrame.pixelFormat = frame.format == TextureFormat.RGBA32 ? 3 : 1;
            GCHandle dataHandle = GCHandle.Alloc(frame.data, GCHandleType.Pinned);
            rawFrame.pixelData = dataHandle.AddrOfPinnedObject();
            rawFrame.fps = mOutVideoFPS;
            int ret = cpp_pipeline_add_raw(dstStreamUUID, rawFrame, IntPtr.Zero, ref frame.pts);
            dataHandle.Free();
            if (ret != 0)
            {
                onStreamLog?.Invoke($"cpp_pipeline_add_raw error, {ret}", LogLevel.Error);
            }
            return ret;
        }

        /// ����Ƶ������Ƶ��Ϊ�������ݣ��Ƴ�һ��ָ��������ָ������Ƶ֡��
        /// ֻ֧�� DX11 ����
        /// ������ UI �̵߳��ã���֤ DX �����İ�ȫ
        /// <param name="srcStreamUUID">Դ�����ƣ���Ҫ���͵������Ǵ������������������</param>
        /// <param name="nativeTexturePtr">ָ�� ID3D11Resource *����ͨ�� Texture.GetNativeTexturePtr ��ȡ��</param>
        /// <param name="pts"></param>
        /// <param name="customData"></param>
        public int SendVideo(string srcStreamUUID, IntPtr nativeTexturePtr, long pts, byte[] customData = null)
        {
            long fps = mOutVideoFPS;
            var outputStream = GetOutputStreamByInputStream(srcStreamUUID);
            if (customData != null)
            {
                GCHandle dataPined = GCHandle.Alloc(customData, GCHandleType.Pinned);
                IntPtr dataPtr = dataPined.AddrOfPinnedObject();
                int ret = cpp_pipeline_add_texture_jce(outputStream, IntPtr.Zero, nativeTexturePtr, 0, ref pts, ref fps, dataPtr, (uint)customData.Length);
                dataPined.Free();
                return ret;
            }
            return cpp_pipeline_add_texture(outputStream, IntPtr.Zero, nativeTexturePtr, 0, ref pts, ref fps);
        }

        /// ����Ƶ������Ƶ��Ϊ�������ݡ�����һ�������ʱʹ�á�
        /// <param name="nativeTexturePtr"></param>
        /// <param name="pts"></param>
        public int SendOneVideo(IntPtr nativeTexturePtr, long pts)
        {
            if (mOutputStreams.Count == 0)
            {
                onStreamLog?.Invoke("There is no output stream, please check your configuration.", LogLevel.Error);
                return -1;
            }
            if (mOutputStreams.Count > 1)
            {
                onStreamLog?.Invoke($"There are more than one output streams in configuration but you don't specify one, use the first output stream.", LogLevel.Warning);
            }
            return SendVideoTo(mOutputStreams[0], nativeTexturePtr, pts);
        }

        /// ����Ƶ���Ƶ�ָ������ַ��
        /// <param name="dstStreamUUID">Ŀ������ַ����������� OutputStreams �б��С�</param>
        /// <param name="nativeTexturePtr"></param>
        /// <param name="pts"></param>
        /// <param name="customData"></param>
        public int SendVideoTo(string dstStreamUUID, IntPtr nativeTexturePtr, long pts, byte[] customData = null)
        {
            long fps = mOutVideoFPS;
            if (customData != null)
            {
                GCHandle dataPined = GCHandle.Alloc(customData, GCHandleType.Pinned);
                IntPtr dataPtr = dataPined.AddrOfPinnedObject();
                int ret = cpp_pipeline_add_texture_jce(dstStreamUUID, IntPtr.Zero, nativeTexturePtr, 0, ref pts, ref fps, dataPtr, (uint)customData.Length);
                dataPined.Free();
                return ret;
            }
            return cpp_pipeline_add_texture(dstStreamUUID, IntPtr.Zero, nativeTexturePtr, 0, ref pts, ref fps);
        }

        /// ����Ƶ��
        /// �����ڴ��� sdk �����ͷš�
        /// <param name="srcStreamUUID">Ŀ������ַ����Ҫ���͵� frame �Ǵ������������������</param>
        /// <param name="frame"></param>
        public int SendAudio(string srcStreamUUID, AudioFrame frame)
        {
            var outputStream = GetOutputStreamByInputStream(srcStreamUUID);
            return SendAudioTo(outputStream, frame);
        }

        /// ����Ƶ��������һ�������ʱʹ�á�
        /// <param name="frame"></param>
        public int SendAudio(AudioFrame frame)
        {
            if (mOutputStreams.Count == 0)
            {
                onStreamLog?.Invoke("There is no output stream, please check your configuration.", LogLevel.Error);
                return -1;
            }
            if (mOutputStreams.Count > 1)
            {
                onStreamLog?.Invoke($"There are more than one output streams in configuration but you don't specify one, use the first output stream.", LogLevel.Warning);
            }
            return SendAudioTo(mOutputStreams[0], frame);
        }

        /// ����Ƶ���Ƶ�ָ������ַ��
        /// <param name="dstStreamUUID">Ŀ������ַ����������� OutputStreams �б��С�</param>
        /// <param name="frame"></param>
        /// <param name="customData"></param>
        public int SendAudioTo(string dstStreamUUID, AudioFrame frame, byte[] customData = null)
        {
            PCMInfo pcm = new PCMInfo();
            pcm.sampleRate = frame.sampleRate;
            pcm.sampleCount = frame.sampleCount;
            pcm.channelLayout = 2;
            pcm.audioFormat = 0;
            GCHandle dataHandle = GCHandle.Alloc(frame.data, GCHandleType.Pinned);
            int ret;
            if (customData != null)
            {
                GCHandle customHandle = GCHandle.Alloc(customData, GCHandleType.Pinned);
                ret = cpp_pipeline_add_pcm_jce(dstStreamUUID, pcm, dataHandle.AddrOfPinnedObject(), (uint)frame.dataSize, ref frame.pts, customHandle.AddrOfPinnedObject(), (uint)customData.Length);
                customHandle.Free();
            }
            else
            {
                ret = cpp_pipeline_add_pcm(dstStreamUUID, pcm, dataHandle.AddrOfPinnedObject(), (uint)frame.dataSize, ref frame.pts);
            }
            dataHandle.Free();
            if (ret != 0)
            {
                onStreamLog?.Invoke($"cpp_pipeline_add_pcm error, {ret}", LogLevel.Error);
            }
            return ret;
        }

        public static string GetLogDirPath()
        {
            var logDirPath = Environment.GetEnvironmentVariable("LOCAL_LOG_DIR");
            if (logDirPath == null)
            {
                logDirPath = System.Environment.CurrentDirectory;
            }

            logDirPath = Encoding.UTF8.GetString(Encoding.Default.GetBytes(logDirPath));
            return logDirPath;
        }

        private string GetOutputStreamByInputStream(string inputStream)
        {
            if (!mMapFromInputStreamToOutputStream.ContainsKey(inputStream))
            {
                mMapFromInputStreamToOutputStream[inputStream] = mOutputStreams[0];
                mOutputStreams.RemoveAt(0);
            }
            return mMapFromInputStreamToOutputStream[inputStream];
        }

        // IL2CPP ��֧�����Ա����ί�е� marshal
        [MonoPInvokeCallback(typeof(CustomDataCallback))]
        private static void OnCustomDataImpl(IntPtr userData, string pipelineId, CustomDataEvent customDataEvent)
        {
            var h = GCHandle.FromIntPtr(userData);
            var sdk = (h.Target as MediaPiplelineControllerSDK);
            sdk.OnCustomData(customDataEvent);
        }

        private void OnCustomData(CustomDataEvent customDataEvent)
        {
            onStreamLog?.Invoke($"MediaPipelineControllerSDK CallBack OnCustomData {customDataEvent.customData} LogTime = {GetLogTime()} Thread =  {Thread.CurrentThread.ManagedThreadId.ToString()}", LogLevel.Info);
            if (onStreamCustomData != null)
            {
                lock (mCustomQueue)
                {
                    mCustomQueue.Enqueue(() =>
                    {
                        onStreamCustomData(customDataEvent);
                    });
                }
            }
        }

        private static string GetLogTime()
        {
            string str = DateTime.Now.ToString("HH:mm:ss.fff") + "";
            return str;
        }

        [MonoPInvokeCallback(typeof(CppCallChannelMsgCallBack))]
        private static void OnCppCallChannelMsgCallBack(IntPtr userData, string msg, uint len)
        {
            var h = GCHandle.FromIntPtr(userData);
            var sdk = (h.Target as MediaPiplelineControllerSDK);
            sdk.OnRecvChannelMsg(msg);
        }

        // ͨ�Žӿڻص�(���������˻�ǰ��С������������)
        private void OnRecvChannelMsg(string msg)
        {
            lock (mChannelMsgQueue)
            {
                mChannelMsgQueue.Enqueue(() =>
                {
                    DealStreamChannelMsgData(msg);
                });
            }
        }

        // ͨ�Ż����ӿ�(���������������˻�ǰ��С���򣬹��ڵײ㣬һ�㲻ֱ�ӵ���)
        public void SendChannelMsg(string jsonStr)
        {
            if (jsonStr == null)
                return;

            byte[] sendData = System.Text.Encoding.UTF8.GetBytes(jsonStr);
            GCHandle dataPined = GCHandle.Alloc(sendData, GCHandleType.Pinned);
            IntPtr dataPtr = dataPined.AddrOfPinnedObject();
            int ret = cpp_call_channel_msg(dataPtr, (uint)sendData.Length);
            dataPined.Free();
            if (ret != 0)
            {
                onStreamLog?.Invoke($"cpp_call_channel_msg error {ret}", LogLevel.Error);
                return;
            }
        }

        // ���������������˻�ǰ��С����ͨ�ýӿ���Ϣ
        public void SendGeneralMsg(string eventName, string reqId, JsonData messageJsonData)
        {
            JsonData jsonChannelMsgData = new JsonData();
            jsonChannelMsgData["eventName"] = eventName;
            jsonChannelMsgData["reqId"] = reqId;
            jsonChannelMsgData["message"] = messageJsonData;
            var jsonChannelMsgStr = jsonChannelMsgData.ToJson();
            SendChannelMsg(jsonChannelMsgStr);
        }

        // ���ƣ�����ǰ��С����Invoke������Ϣ(��С����JS������������ؽӿ�)
        public void SendAppletInvokeReqMsg(string appletApiName, List<string> appletApiParamArray, string reqId)
        {
            JsonData messageJsonData = new JsonData();
            JsonData msgJsonData = new JsonData();
            msgJsonData["type"] = "CALL_JS_SDK_REQ";

            var paramsJsonData = new JsonData();
            paramsJsonData.SetJsonType(JsonType.Array);
            foreach (var appletApiParam in appletApiParamArray)
            {
                paramsJsonData.Add(appletApiParam);
            }

            JsonData payloadJsonData = new JsonData();
            payloadJsonData["reqId"] = reqId;
            payloadJsonData["apiName"] = appletApiName;
            payloadJsonData["params"] = paramsJsonData;
            msgJsonData["payload"] = payloadJsonData;

            messageJsonData["msg"] = msgJsonData.ToJson();
            SendGeneralMsg("SendMessageToApplet", reqId, messageJsonData);
        }

        // ���ƣ�����С������ü�����Ϣ�ӿ�(��С����JS�ļ�����ؽӿ�)
        public void SendAppletInvokeListenMsg(
            string appletApiName, List<string> appletApiParamArray,
            string reqId, string listenOnOff, string eventType)
        {
            JsonData messageJsonData = new JsonData();
            JsonData msgJsonData = new JsonData();
            msgJsonData["type"] = "CALL_JS_SDK_REQ";

            var paramsJsonData = new JsonData();
            paramsJsonData.SetJsonType(JsonType.Array);
            foreach (var param in appletApiParamArray)
            {
                paramsJsonData.Add(param);
            }

            JsonData payloadJsonData = new JsonData();
            payloadJsonData["reqId"] = reqId;
            payloadJsonData["apiName"] = appletApiName;
            payloadJsonData["params"] = paramsJsonData;
            payloadJsonData["callType"] = listenOnOff;
            payloadJsonData["eventType"] = eventType;
            msgJsonData["payload"] = payloadJsonData;

            messageJsonData["msg"] = msgJsonData.ToJson();
            SendGeneralMsg("SendMessageToApplet", reqId, messageJsonData);
        }

        private void DealStreamChannelMsgData(string message)
        {
            if (message == null)
            {
                return;
            }

            JsonData jsonData = JsonMapper.ToObject(message);
            if (!jsonData.ContainsKey("message") ||
               !jsonData.ContainsKey("eventName") ||
               !jsonData.ContainsKey("reqId") ||
               !jsonData.ContainsKey("res"))
            {
                return;
            }

            var reqId = (string)jsonData["reqId"];
            var res = (int)jsonData["res"];

            // ��Բ�ͬeventName��������ͬ��message���ݣ����ĵ�˵����Ӧ��������
            JsonData messageJsonData = jsonData["message"];
            var eventName = (string)jsonData["eventName"];
            if (eventName == "SendMessageToApplet_Callback") // һ��ɺ���
            {
                //if (messageJsonData.ContainsKey("msg"))
                //{
                //    var msg = (string)messageJsonData["msg"];
                //    Debug.Log("get applet channel msg, eventName: " + eventName + ", msg: " + msg);
                //}
            }
            else if (eventName == "OnAppletMessage") // ǰ��С���򷢹�����Ϣ
            {
                if (messageJsonData.ContainsKey("msg"))
                {
                    var msg = (string)messageJsonData["msg"];
                    // ������С����invoke����ͨ��򳣹�ͨ����Ϣ
                    JsonData msgJsonData = JsonMapper.ToObject(msg);
                    if (msgJsonData.IsObject)
                    {
                        ParseAppletInvokeMessage(msgJsonData);
                    }
                    else
                    {
                        ParseAppletMessage(msg);
                    }
                }
            }
            else
            {
                if (onAnchorMsg != null)
                {
                    onAnchorMsg(eventName, messageJsonData);
                }
            }
        }

        private void ParseAppletInvokeMessage(JsonData msgJsonData)
        {
            // С����invoke����ͨ��
            if (msgJsonData.ContainsKey("name") &&
                "GameMsg" == (string)msgJsonData["name"] &&
                msgJsonData.ContainsKey("message"))
            {
                JsonData internalMessageJsonData = msgJsonData["message"];
                if (internalMessageJsonData.ContainsKey("type") &&
                    internalMessageJsonData.ContainsKey("payload"))
                {
                    var type = (string)internalMessageJsonData["type"];
                    if ("CALL_JS_SDK_RSP" == type) // С����invoke����Ӧ��ͨ��
                    {
                        var playloadJsonData = internalMessageJsonData["payload"];
                        if (playloadJsonData.ContainsKey("apiName") &&
                            playloadJsonData.ContainsKey("reqId"))
                        {
                            var apiName = (string)playloadJsonData["apiName"];
                            var internalReqId = (string)playloadJsonData["reqId"];
                            var rsp = "";
                            var err = "";
                            if (playloadJsonData.ContainsKey("rsp"))
                            {
                                rsp = (string)playloadJsonData["rsp"];
                            }

                            if (playloadJsonData.ContainsKey("err"))
                            {
                                var errJsonData = playloadJsonData["err"];
                                err = errJsonData.ToJson();
                                if (err == "")
                                {
                                    err = "occur unexpected error.";
                                }
                            }

                            if (onAppletInvokeRspMsg != null)
                            {
                                onAppletInvokeRspMsg(apiName, rsp, internalReqId, err);
                            }
                        }
                    }
                    else if ("CALL_JS_SDK_EVENT" == type) // С����invoke����ͨ��
                    {
                        var playloadJsonData = internalMessageJsonData["payload"];
                        if (playloadJsonData.ContainsKey("apiName") &&
                            playloadJsonData.ContainsKey("message"))
                        {
                            var apiName = (string)playloadJsonData["apiName"];
                            var messageStr = (string)playloadJsonData["message"];
                            var cbId = "";
                            if (playloadJsonData.ContainsKey("cbId"))
                            {
                                cbId = (string)playloadJsonData["cbId"];
                            }
                            
                            if (onAppletInvokeListenRspMsg != null)
                            {
                                onAppletInvokeListenRspMsg(apiName, messageStr, cbId);
                            }
                        }
                    }
                }
            }
        }

        private void ParseAppletMessage(string message)
        {
            if (onAppletMsg != null)
            {
                onAppletMsg(message);
            }
        }
    }
}
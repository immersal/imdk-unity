using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Immersal;
using Immersal.Samples.Util;
using Immersal.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

public class CustomLocalization : MonoBehaviour
{
    [SerializeField]
    private Button m_LocalizeButton;

    [SerializeField]
    private TextMeshProUGUI m_ImageCountText;

    [SerializeField]
    private NotificationManager m_NotificationManager;
    
    [SerializeField]
    private int m_LocalizationCount = 3;
    
    [SerializeField]
    private bool m_FlipImage = false;

    private ImmersalSDK sdk;
    private List<IPlatformUpdateResult> m_PlatformUpdateResults = new List<IPlatformUpdateResult>();

    private void Start()
    {
        sdk = ImmersalSDK.Instance;
        if (!sdk)
        {
            gameObject.SetActive(false);
            return;
        }

        if (m_LocalizeButton)
            m_LocalizeButton.onClick.AddListener(BufferedLocalization);
    }

    private void OnDestroy()
    {
        if (m_LocalizeButton)
            m_LocalizeButton.onClick.RemoveListener(BufferedLocalization);
    }

    public void BufferedLocalization()
    {
        _ = BufferAndLocalize();
    }
    
    private async Task BufferAndLocalize()
    {
        // We're going to fetch data from the PlatformSupport and buffer it
        m_PlatformUpdateResults.Add(await sdk.PlatformSupport.UpdatePlatform());

        UpdateGUI();
        
        // Bail out if we don't have enough data yet
        if (m_PlatformUpdateResults.Count < m_LocalizationCount) return;
        
        // When we have enough data, we proceed to localize with all of it
        
        // Start and collect localization tasks
        List<Task<bool>> localizationTasks = new List<Task<bool>>();
        foreach (IPlatformUpdateResult platformUpdateResult in m_PlatformUpdateResults)
        {
            localizationTasks.Add(Localize(platformUpdateResult));
        }
        
        // Wait for all collected tasks to complete
        bool[] results = await Task.WhenAll(localizationTasks);
        int successCount = results.Count(r => r);

        // Report on results
        if (m_NotificationManager)
        {
            if (successCount > 0)
            {
                m_NotificationManager.GenerateSuccess($"Successfully localized {successCount}/{m_LocalizationCount}");
            }
            else
            {
                m_NotificationManager.GenerateWarning("No success");
            }
        }
        
        // Clear buffer
        m_PlatformUpdateResults.Clear();
        
        UpdateGUI();
    }

    private async Task<bool> Localize(IPlatformUpdateResult platformUpdateResult)
    {
        if (!platformUpdateResult.Success) return false;
        
        // Grab the CameraData from the PlatformSupport update result
        ICameraData cameraData = platformUpdateResult.CameraData;
        
        // Do something with the data
        CameraData adjustedData = await DoSomethingWithData(cameraData);
        
        // Localize with the data
        // This will complete once all tasks from currently configured LocalizationMethods complete
        ILocalizationResults locResults = await ImmersalSDK.Instance.Localizer.LocalizeAllMethods(adjustedData);
        
        // Number of results depends on how XRMaps and LocalizationMethods are configured
        // Iterate results and update scenes accordingly.
        bool hadSuccess = false;
        foreach (ILocalizationResult result in locResults.Results)
        {
            // Skip unsuccessful localizations
            if (!result.Success) continue;
            
            // Check that the map we localized to is still loaded
            if (!MapManager.TryGetMapEntry(result.MapId, out MapEntry entry)) continue;
            
            // Update the scene
            await sdk.SceneUpdater.UpdateScene(entry, adjustedData, result);
            
            hadSuccess = true;
        }
        
        // Update tracking status
        sdk.TrackingAnalyzer.Analyze(platformUpdateResult.Status, locResults);

        return hadSuccess;
    }
    
    private async Task<CameraData> DoSomethingWithData(ICameraData data)
    {
        // Get access to IImageData, a platform specific implementation that owns the original data.
        // In addition, the CameraData.GetImageData() call will increment the internal reference count.
        // The using statement will dispose the IImageData reference at the end of the scope,
        // decreasing the reference count in CameraData.
        // Finally, if the last reference was disposed, CameraData will dispose the original data.
        using IImageData imageData = data.GetImageData();
 
        // We can get a pointer to the image data.
        // Note that this could point to data that has been manipulated internally
        // instead of directly to the original data (XRCPUImage in the case of AR Foundation).
        IntPtr pointer = imageData.UnmanagedDataPointer;
        
        // We can cast the IImageData reference to a specific implementation to access the original data.
        // It's important to check if the cast results in a valid object, pattern variables are great for this.
        if (imageData is ARFImageData arf)
        {
            XRCpuImage image = arf.Image;
            
            // Do something with the image..
        }
        
        // We can also get a managed copy of the bytes with a convenience method.
        byte[] bytes = data.GetBytes();
 
        // We can now alter the data as we'd like.
        if (m_FlipImage)
            await VerticallyFlipImageUnsafe(bytes, data.Width, data.Height, data.Channels);
        
        // We cannot directly make changes to the CameraData as it is intended to be immutable.
        // Instead, we can create a copy with our altered data injected into it.
        CameraData newData = data.Copy(new SimpleImageData(bytes));
        
        // Note that the newly created CameraData must get disposed at some point in the future.
        // If it gets used in localization, it will eventually happen automatically thanks to the
        // internal reference counting mechanism.
        
        return newData;
    }
    
    private static unsafe Task VerticallyFlipImageUnsafe(byte[] imageData, int width, int height, int channels)
    {
        fixed (byte* ptr = imageData)
        {
            int stride = width * channels;
            byte* top = ptr;
            byte* bottom = ptr + (height - 1) * stride;
            byte* temp = stackalloc byte[stride];

            for (int y = 0; y < height / 2; y++)
            {
                Buffer.MemoryCopy(top, temp, stride, stride);
                Buffer.MemoryCopy(bottom, top, stride, stride);
                Buffer.MemoryCopy(temp, bottom, stride, stride);
                top += stride;
                bottom -= stride;
            }
        }

        return Task.CompletedTask;
    }

    private void UpdateGUI()
    {
        m_ImageCountText?.SetText($"Images: {m_PlatformUpdateResults.Count}/{m_LocalizationCount}");
    }

}

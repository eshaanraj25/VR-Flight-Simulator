/************************************************************************************

Copyright   :   Copyright 2014 Oculus VR, LLC. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.3 (the "License");
you may not use the Oculus VR Rift SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

http://www.oculus.com/licenses/LICENSE-3.3

Unless required by applicable law or agreed to in writing, the Oculus VR SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using VR = UnityEngine.XR;

/// <summary>
/// Add OVROverlay script to an object with an optional mesh primitive
/// rendered as a TimeWarp overlay instead by drawing it into the eye buffer.
/// This will take full advantage of the display resolution and avoid double
/// resampling of the texture.
/// 
/// If the texture is dynamically generated, as for an interactive GUI or
/// animation, it must be explicitly triple buffered to avoid flickering
/// when it is referenced asynchronously by TimeWarp, check OVRRTOverlayConnector.cs for triple buffers design
/// 
/// We support 3 types of Overlay shapes right now
///		1. Quad : This is most common overlay type , you render a quad in Timewarp space.
///		2. Cylinder: [Mobile Only][Experimental], Display overlay as partial surface of a cylinder
///			* The cylinder's center will be your game object's center
///			* We encoded the cylinder's parameters in transform.scale, 
///				**[scale.z] is the radius of the cylinder
///				**[scale.y] is the height of the cylinder
///				**[scale.x] is the length of the arc of cylinder
///		* Limitations
///				**Only the half of the cylinder can be displayed, which means the arc angle has to be smaller than 180 degree,  [scale.x] / [scale.z] <= PI
///				**Your camera has to be inside of the inscribed sphere of the cylinder, the overlay will be faded out automatically when the camera is close to the inscribed sphere's surface.
///				**Translation only works correctly with vrDriver 1.04 or above
///		3. Cubemap: [Mobile Only], Display overlay as a cube map
/// </summary>

public enum OverlayShape
{
	Quad = 0,       // Display overlay as a quad
	Cylinder = 1,   // [Mobile Only][Experimental] Display overlay as a cylinder, Translation only works correctly with vrDriver 1.04 or above 
	Cubemap = 2,    // [Mobile Only] Display overlay as a cube map
}

public class OVROverlay : MonoBehaviour
{
	public enum OverlayType
	{
		None,           // Disabled the overlay
		Underlay,       // Eye buffers blend on top
		Overlay,        // Blends on top of the eye buffer
		OverlayShowLod  // (Deprecated) Blends on top and colorizes texture level of detail
	};

#if UNITY_ANDROID && !UNITY_EDITOR
	const int maxInstances = 3;
#else
	const int maxInstances = 15;
#endif

	static OVROverlay[] instances = new OVROverlay[maxInstances];
	OverlayType currentOverlayType = OverlayType.Overlay;

	/// <summary>
	/// Specify overlay's shape
	/// </summary>
	public OverlayShape currentOverlayShape = OverlayShape.Quad;

	/// <summary>
	/// Try to avoid setting texture frequently when app is running, texNativePtr updating is slow since rendering thread synchronization
	/// Please cache your nativeTexturePtr and use  OverrideOverlayTextureInfo
	/// </summary>
	public Texture texture = null;
	private Texture cachedTexture = null;
	private IntPtr texNativePtr = IntPtr.Zero;
	private int layerIndex = -1;
	Renderer rend;

	/// <summary>
	/// Use this function to set texture and texNativePtr when app is running 
	/// GetNativeTexturePtr is a slow behavior, the value should be pre-cached 
	/// </summary>
	public void OverrideOverlayTextureInfo(Texture srcTexture, IntPtr nativePtr)
	{
		texture = srcTexture;
		cachedTexture = texture;
		texNativePtr = nativePtr;
	}

	void Awake()
	{
		Debug.Log("Overlay Awake");
		rend = GetComponent<Renderer>();
		if (texture)
		{
			cachedTexture = texture;
			texNativePtr = texture.GetNativeTexturePtr();
		}
		else if (rend) // Backward compitability
		{
			texture = rend.material.mainTexture;
			cachedTexture = texture;
			texNativePtr = texture.GetNativeTexturePtr();
		}
	}

	void OnEnable()
	{
		if (!OVRManager.isHmdPresent)
		{
			enabled = false;
			return;
		}

		OnDisable();

		for (int i = 0; i < maxInstances; ++i)
		{
			if (instances[i] == null || instances[i] == this)
			{
				layerIndex = i;
				instances[i] = this;
				break;
			}
		}
	}

	void OnDisable()
	{
		if (layerIndex != -1)
		{
			// Turn off the overlay if it was on.
			OVRPlugin.SetOverlayQuad(true, false, IntPtr.Zero, IntPtr.Zero, OVRPose.identity.ToPosef(), Vector3.one.ToVector3f(), layerIndex);
			instances[layerIndex] = null;
		}
		layerIndex = -1;
	}

	void OnRenderObject()
	{
		// The overlay must be specified every eye frame, because it is positioned relative to the
		// current head location.  If frames are dropped, it will be time warped appropriately,
		// just like the eye buffers.
		if (Camera.current != Camera.main || Camera.current.cameraType != CameraType.Game || layerIndex == -1 || currentOverlayType == OverlayType.None)
			return;

#if !UNITY_ANDROID || UNITY_EDITOR
		if (currentOverlayShape == OverlayShape.Cubemap || currentOverlayShape == OverlayShape.Cylinder)
		{
			Debug.LogWarning("Overlay shape " + currentOverlayShape + " is not supported on current platform");
		}
#endif

		if (texture != cachedTexture)
		{
			cachedTexture = texture;
			if (cachedTexture)
				texNativePtr = cachedTexture.GetNativeTexturePtr();
		}

		if (cachedTexture == null || texNativePtr == IntPtr.Zero)
			return;

		if (currentOverlayShape == OverlayShape.Cubemap)
		{
			if (texture.GetType() != typeof(Cubemap))
			{
				Debug.LogError("Need Cubemap texture for cube map overlay");
				return;
			}
		}

		bool overlay = (currentOverlayType == OverlayType.Overlay);
		bool headLocked = false;
		for (var t = transform; t != null && !headLocked; t = t.parent)
			headLocked |= (t == Camera.current.transform);

		OVRPose pose = (headLocked) ? transform.ToHeadSpacePose() : transform.ToTrackingSpacePose();
		Vector3 scale = transform.lossyScale;
		for (int i = 0; i < 3; ++i)
			scale[i] /= Camera.current.transform.lossyScale[i];

		// Cylinder overlay sanity checking
		if (currentOverlayShape == OverlayShape.Cylinder)
		{
			float arcAngle = scale.x / scale.z / (float)Math.PI * 180.0f;
			if (arcAngle > 180.0f)
			{
				Debug.LogError("Cylinder overlay's arc angle has to be below 180 degree, current arc angle is " + arcAngle + " degree." );
				return ;
			}
		}

		bool isOverlayVisible = OVRPlugin.SetOverlayQuad(overlay, headLocked, texNativePtr, IntPtr.Zero, pose.flipZ().ToPosef(), scale.ToVector3f(), layerIndex, currentOverlayShape);
		if (rend)
			rend.enabled = !isOverlayVisible;
	}

}

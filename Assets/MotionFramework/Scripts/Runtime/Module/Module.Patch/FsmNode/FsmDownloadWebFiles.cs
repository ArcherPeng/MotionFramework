﻿//--------------------------------------------------
// Motion Framework
// Copyright©2019-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using MotionFramework.AI;
using MotionFramework.Resource;
using MotionFramework.Network;
using MotionFramework.Utility;

namespace MotionFramework.Patch
{
	internal class FsmDownloadWebFiles : IFsmNode
	{
		private readonly PatchManagerImpl _patcher;
		public string Name { private set; get; }

		public FsmDownloadWebFiles(PatchManagerImpl patcher)
		{
			_patcher = patcher;
			Name = EPatchStates.DownloadWebFiles.ToString();
		}
		void IFsmNode.OnEnter()
		{
			PatchEventDispatcher.SendPatchStatesChangeMsg(EPatchStates.DownloadWebFiles);
			MotionEngine.StartCoroutine(Download());
		}
		void IFsmNode.OnUpdate()
		{
		}
		void IFsmNode.OnExit()
		{
		}
		void IFsmNode.OnHandleMessage(object msg)
		{
		}

		private IEnumerator Download()
		{
			// 注意：开发者需要在下载前检测磁盘空间不足

			// 计算下载文件的总大小
			int totalDownloadCount = _patcher.DownloadList.Count;
			long totalDownloadSizeBytes = 0;
			foreach (var element in _patcher.DownloadList)
			{
				totalDownloadSizeBytes += element.SizeBytes;
			}

			// 开始下载列表里的所有资源
			MotionLog.Log($"Begine download web files : {_patcher.DownloadList.Count}");
			long currentDownloadSizeBytes = 0;
			int currentDownloadCount = 0;
			foreach (var element in _patcher.DownloadList)
			{
				// 注意：资源版本号只用于确定下载路径
				string url = _patcher.GetWebDownloadURL(element.Version.ToString(), element.Name);
				string savePath = AssetPathHelper.MakePersistentLoadPath(element.MD5);
				FileUtility.CreateFileDirectory(savePath);

				// 创建下载器
				MotionLog.Log($"Beginning to download web file : {savePath}");
				WebFileRequest download = new WebFileRequest(url, savePath);
				download.DownLoad();
				yield return download; //文件依次加载（在一个文件加载完毕后加载下一个）				

				// 检测是否下载失败
				if (download.HasError())
				{
					download.ReportError();
					download.Dispose();
					PatchEventDispatcher.SendWebFileDownloadFailedMsg(url, element.Name);
					yield break;
				}

				// 立即释放加载器
				download.Dispose();
				currentDownloadCount++;
				currentDownloadSizeBytes += element.SizeBytes;
				PatchEventDispatcher.SendDownloadFilesProgressMsg(totalDownloadCount, currentDownloadCount, totalDownloadSizeBytes, currentDownloadSizeBytes);
			}

			// 验证下载文件
			foreach (var element in _patcher.DownloadList)
			{
				if (_patcher.CheckPatchFileValid(element) == false)
				{
					MotionLog.Error($"Patch file is invalid : {element.Name}");
					PatchEventDispatcher.SendWebFileCheckFailedMsg(element.Name);
					yield break;
				}
			}

			// 最后保存最新的补丁清单
			_patcher.SaveWebPatchManifest();

			// 最后清空下载列表
			_patcher.DownloadList.Clear();
			_patcher.SwitchNext();
		}
	}
}
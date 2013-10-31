using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
//using System.Net.Http;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Octokit;

namespace GitHubTasks
{
    /// <summary>
    /// Create a release on github.com.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// <ItemGroup>
    ///   <ReleaseFiles Include="MyAwesomeProject-v0.1.0.zip" />
    ///   <ReleaseFiles Include="MyAwesomeReleaseNotes.md">
    ///     <ContentType>text/plain</ContentType>
    ///   </ReleaseFiles>
    /// </ItemGroup>
    /// <ItemGroup>
    ///   <ReleaseNotesFile Include="MyAwesomeReleaseNotes.md" />
    /// </ItemGroup>
    /// <Target Name="Release">
    ///   <CreateGitHubRelease Repository="owner/repo" TagName="v0.1.0" Files="@(ReleaseFiles)" ReleaseNotesFile="@(ReleaseNotesFile)" />
    /// </Target>
    /// ]]></code>
    /// </example>
    public class CreateRelease : Task
    {
        [Required]
        public string Repository { get; set; }

        [Required]
        public string TagName { get; set; }

        public ITaskItem[] Files { get; set; }

        public ITaskItem ReleaseNotesFile { get; set; }

        [Output]
        public ITaskItem[] UploadedAssets { get; private set; }

        private string Owner { get { return Repository.Split('/')[0]; } }

        private string RepositoryName { get { return Repository.Split('/')[1]; } }

        private ReleaseUpdate ReleaseData { get { return new ReleaseUpdate(TagName); } }

        public override bool Execute()
        {
            var client = new GitHubClient(new ProductHeaderValue("GitHubTasks")).Release;
            var release = client.CreateRelease(Owner, RepositoryName, ReleaseData).Result;
            Log.LogMessage("Created Release {0} at {1}", release.Id, release.HtmlUrl);
            var assetTasks = new List<System.Threading.Tasks.Task<ReleaseAsset>>();
            foreach (var item in Files)
            {
                Log.LogMessage("Uploading {0}...", item.ItemSpec);
                assetTasks.Add(client.UploadAsset(release, BuildAssetUpload(item)));
            }
            var uploadedAssets = new List<ITaskItem>();
            foreach (var assetTask in assetTasks)
            {
                var asset = assetTask.Result;
                Log.LogMessage("{0} was uploaded.", asset.Url);
                uploadedAssets.Add(TaskItemFor(asset));
            }
            UploadedAssets = uploadedAssets.ToArray();
            return true;
        }

        private ReleaseAssetUpload BuildAssetUpload(ITaskItem item)
        {
            var data = new ReleaseAssetUpload();
            data.ContentType = item.GetMetadata("ContentType");
            if (data.ContentType == null) data.ContentType = "application/octet-stream";
            data.FileName = Path.GetFileName(item.ItemSpec);
            data.RawData = File.OpenRead(item.ItemSpec);
            return data;
        }

        private ITaskItem TaskItemFor(ReleaseAsset asset)
        {
            var item = new TaskItem();
            item.ItemSpec = asset.Url;
            item.SetMetadata("ContentType", asset.ContentType);
            item.SetMetadata("Id", asset.Id.ToString());
            item.SetMetadata("Label", asset.Label);
            item.SetMetadata("Name", asset.Name);
            item.SetMetadata("State", asset.State);
            return item;
        }
    }
}
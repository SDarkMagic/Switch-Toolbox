﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library.Forms;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;
using Toolbox.Library.IO;
using Toolbox.Library;
using FirstPlugin;
using LayoutBXLYT.Cafe;

namespace LayoutBXLYT
{
    public partial class LayoutEditor : Form
    {
        /// <summary>
        /// Enables or disables legacy opengl support
        /// Modern support is not quite finished yet so keep enabled!
        /// </summary>
        public static bool UseLegacyGL = true;

        public LayoutViewer GamePreviewWindow;

        private LayoutCustomPaneMapper CustomMapper;

        private Dictionary<string, STGenericTexture> Textures;

        public List<BxlytHeader> LayoutFiles = new List<BxlytHeader>();
        public List<BxlanHeader> AnimationFiles = new List<BxlanHeader>();

        private BxlytHeader ActiveLayout;
        private BxlanHeader ActiveAnimation;

        public enum DockLayout
        {
            Default,
            Animation,
        }

        public EventHandler ObjectSelected;
        public EventHandler ObjectChanged;

        public LayoutEditor()
        {
            InitializeComponent();
            CustomMapper = new LayoutCustomPaneMapper();

            Textures = new Dictionary<string, STGenericTexture>();

            var theme = new VS2015DarkTheme();
            this.dockPanel1.Theme = theme;
            this.dockPanel1.BackColor = FormThemes.BaseTheme.FormBackColor;
            this.BackColor = FormThemes.BaseTheme.FormBackColor;

            viewportBackColorCB.Items.Add("Back Color : Default");
            viewportBackColorCB.Items.Add("Back Color : Custom");
            orthographicViewToolStripMenuItem.Checked = true;

            foreach (var type in Enum.GetValues(typeof(Runtime.LayoutEditor.DebugShading)).Cast<Runtime.LayoutEditor.DebugShading>())
                debugShading.Items.Add(type);

            debugShading.SelectedItem = Runtime.LayoutEditor.Shading;
            displayNullPanesToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayNullPane;
            displayyBoundryPanesToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayBoundryPane;
            displayPicturePanesToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayPicturePane;
            displayWindowPanesToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayWindowPane;
            renderInGamePreviewToolStripMenuItem.Checked = Runtime.LayoutEditor.IsGamePreview;
            displayGridToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayGrid;
            displayTextPanesToolStripMenuItem.Checked = Runtime.LayoutEditor.DisplayTextPane;

            ObjectSelected += OnObjectSelected;
            ObjectChanged += OnObjectChanged;

            if (Runtime.LayoutEditor.BackgroundColor != Color.FromArgb(130, 130, 130))
                viewportBackColorCB.SelectedIndex = 1;
            else
                viewportBackColorCB.SelectedIndex = 0;
        }

        private List<LayoutViewer> Viewports = new List<LayoutViewer>();
        private LayoutViewer ActiveViewport;
        private LayoutHierarchy LayoutHierarchy;
        private LayoutHierarchy AnimLayoutHierarchy;
        private LayoutTextureList LayoutTextureList;
        private LayoutProperties LayoutProperties;
        private LayoutTextDocked TextConverter;
        private LayoutPartsEditor LayoutPartsEditor;
        private STAnimationPanel AnimationPanel;

        private bool isLoaded = false;
        public void LoadBxlyt(BxlytHeader header)
        {
         /*   if (PluginRuntime.BxfntFiles.Count > 0)
            {
                var result = MessageBox.Show("Found font files opened. Would you like to save character images to disk? " +
                    "(Allows for faster loading and won't need to be reopened)", "Layout Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    var cacheDir = $"{Runtime.ExecutableDir}/Cached/Font";
                    if (!System.IO.Directory.Exists(cacheDir))
                        System.IO.Directory.CreateDirectory(cacheDir);

                    foreach (var bffnt in PluginRuntime.BxfntFiles)
                    {
                        if (!System.IO.Directory.Exists($"{cacheDir}/{bffnt.Name}"))
                            System.IO.Directory.CreateDirectory($"{cacheDir}/{bffnt.Name}");

                        var fontBitmap = bffnt.GetBitmapFont(true);
                        foreach (var character in fontBitmap.Characters)
                        {
                            var charaMap = character.Value;
                            charaMap.CharBitmap.Save($"{cacheDir}/{bffnt.Name}/cmap_{Convert.ToUInt16(character.Key).ToString("x2")}_{charaMap.CharWidth}_{charaMap.GlyphWidth}_{charaMap.LeftOffset}.png");
                        }

                        fontBitmap.Dispose();
                    }
                }
            }*/

            LayoutFiles.Add(header);
            ActiveLayout = header;

            if (ActiveViewport == null)
            {
                LayoutViewer Viewport = new LayoutViewer(this,header, Textures);
                Viewport.DockContent = new DockContent();
                Viewport.DockContent.Controls.Add(Viewport);
                Viewport.Dock = DockStyle.Fill;
                Viewport.DockContent.Show(dockPanel1, DockState.Document);
                Viewport.DockContent.DockHandler.AllowEndUserDocking = false;
                Viewports.Add(Viewport);
                ActiveViewport = Viewport;
            }
            else
                ActiveViewport.LoadLayout(header);

            orthographicViewToolStripMenuItem.Checked = Runtime.LayoutEditor.UseOrthographicView;

            if (!isLoaded)
                InitializeDockPanels();

            AnimLayoutHierarchy?.SearchAnimations(header, ObjectSelected);

            CustomMapper.LoadMK8DCharaSelect(Textures, header);

            isLoaded = true;
        }

        public void LoadBxlan(BxlanHeader header)
        {
            AnimationFiles.Add(header);
            ActiveAnimation = header;

            ShowAnimationHierarchy();
            ShowPropertiesPanel();
            AnimLayoutHierarchy.LoadAnimation(ActiveAnimation, ObjectSelected);

            isLoaded = true;
        }

        private void InitializeDockPanels(bool isAnimation = false)
        {
            ShowAnimationHierarchy();
            ShowTextureList();
            ShowPartsEditor();
            ShowPaneHierarchy();
            ShowPropertiesPanel();
            UpdateBackColor();
            ShowAnimationPanel();
        }

        private void ResetEditors()
        {
            if (LayoutHierarchy != null)
                LayoutHierarchy.Reset();
            if (LayoutTextureList != null)
                LayoutTextureList.Reset();
            if (LayoutProperties != null)
                LayoutProperties.Reset();
            if (TextConverter != null)
                TextConverter.Reset();
        }

        private void ReloadEditors(BxlytHeader activeLayout)
        {
            if (!isLoaded) return;

            if (LayoutProperties != null)
                LayoutProperties.Reset();
            if (LayoutHierarchy != null)
                LayoutHierarchy.LoadLayout(activeLayout, ObjectSelected);
            if (LayoutTextureList != null)
                LayoutTextureList.LoadTextures(activeLayout);
            if (TextConverter != null)
            {
                if (ActiveLayout.FileInfo is BFLYT)
                    TextConverter.LoadLayout((BFLYT)ActiveLayout.FileInfo);
            }
        }

        private void OnObjectChanged(object sender, EventArgs e)
        {

        }

        private void OnProperyChanged()
        {
            Console.WriteLine("UpdateProperties");

            if (LayoutProperties != null)
                LayoutProperties.UpdateProperties();

            if (ActiveViewport != null)
                ActiveViewport.UpdateViewport();
        }

        private bool isChecked = false;
        private void OnObjectSelected(object sender, EventArgs e)
        {
            if (isChecked) return;

            if (AnimationPanel != null)
            {
                if (e is TreeViewEventArgs)
                {
                    var node = ((TreeViewEventArgs)e).Node;
                    if (node.Tag is ArchiveFileInfo) {
                        UpdateAnimationNode(node);
                    }

                    if (node.Tag is BxlanHeader)
                        UpdateAnimationPlayer((BxlanHeader)node.Tag);
                }
            }
            if (LayoutProperties != null && (string)sender == "Select")
            {
                ActiveViewport?.SelectedPanes.Clear();

                if (e is TreeViewEventArgs) {
                    var node = ((TreeViewEventArgs)e).Node;

                    if (node.Tag is BasePane)
                    {
                        var pane = node.Tag as BasePane;
                        LayoutProperties.LoadProperties(pane, OnProperyChanged);
                        ActiveViewport?.SelectedPanes.Add(pane);
                    }
                    else
                        LayoutProperties.LoadProperties(node.Tag, OnProperyChanged);
                }
            }
            if (ActiveViewport != null)
            {
                if (e is TreeViewEventArgs && (string)sender == "Checked" && !isChecked) {
                    isChecked = true;
                    var node = ((TreeViewEventArgs)e).Node;
                    ToggleChildern(node, node.Checked);
                    isChecked = false;
                }

                ActiveViewport.UpdateViewport();
            }
        }

        private void UpdateAnimationNode(TreeNode node)
        {
            node.Nodes.Clear();

            var archiveNode = node.Tag as ArchiveFileInfo;
            var fileFormat = archiveNode.OpenFile();

            //Update the tag so this doesn't run again
            node.Tag = "Expanded";

            if (fileFormat != null && fileFormat is BXLAN)
            {
                node.Tag = ((BXLAN)fileFormat).BxlanHeader;

                LayoutHierarchy.LoadAnimations(((BXLAN)fileFormat).BxlanHeader, node, false);
                AnimationFiles.Add(((BXLAN)fileFormat).BxlanHeader);
            }
        }

        private void ToggleChildern(TreeNode node, bool isChecked)
        {
            if (node.Tag is BasePane)
                ((BasePane)node.Tag).DisplayInEditor = isChecked;

            node.Checked = isChecked;
            foreach (TreeNode child in node.Nodes)
                ToggleChildern(child, isChecked);
        }

        private void UpdateAnimationPlayer(BxlanHeader animHeader)
        {
            if (AnimationPanel == null) return;

            if (ActiveLayout != null)
            {
                AnimationPanel.Reset();
                AnimationPanel.AddAnimation(animHeader.ToGenericAnimation(ActiveLayout), true);
                //   foreach (var anim in AnimationFiles)
                //     AnimationPanel.AddAnimation(anim.ToGenericAnimation(ActiveLayout), false);
            }
        }

        private void LayoutEditor_ParentChanged(object sender, EventArgs e)
        {
            if (this.ParentForm == null) return;
        }

        private void textureListToolStripMenuItem_Click(object sender, EventArgs e) {
            ShowTextureList();
        }

        private void ShowPartsEditor()
        {
            if (LayoutPartsEditor != null)
                return;

            LayoutPartsEditor = new LayoutPartsEditor();
            LayoutPartsEditor.Text = "Parts Editor";
            LayoutPartsEditor.Show(dockPanel1, DockState.DockLeft);
        }

        private void ShowPropertiesPanel()
        {
            if (LayoutProperties != null)
                return;

            LayoutProperties = new LayoutProperties();
            LayoutProperties.Text = "Properties";
            if (LayoutHierarchy != null)
                LayoutProperties.Show(LayoutHierarchy.Pane, DockAlignment.Bottom, 0.5);
            else
                LayoutProperties.Show(dockPanel1, DockState.DockRight);
        }

        public void ShowAnimationHierarchy()
        {
            if (AnimLayoutHierarchy != null)
                return;

            AnimLayoutHierarchy = new LayoutHierarchy(this);
            AnimLayoutHierarchy.Text = "Animation Hierarchy";
            AnimLayoutHierarchy.Show(dockPanel1, DockState.DockLeft);
        }

        private void ShowPaneHierarchy()
        {
            if (LayoutHierarchy != null)
                return;

            LayoutHierarchy = new LayoutHierarchy(this);
            LayoutHierarchy.Text = "Hierarchy";
            LayoutHierarchy.LoadLayout(ActiveLayout, ObjectSelected);
            LayoutHierarchy.Show(dockPanel1, DockState.DockLeft);
        }

        private void ShowTextureList()
        {
            if (LayoutTextureList != null)
                return;

            LayoutTextureList = new LayoutTextureList();
            LayoutTextureList.Text = "Texture List";
            LayoutTextureList.LoadTextures(ActiveLayout);
            LayoutTextureList.Show(dockPanel1, DockState.DockRight);
        }

        public void ShowAnimationPanel()
        {
            DockContent dockContent = new DockContent();
            AnimationPanel = new STAnimationPanel();
            AnimationPanel.Dock = DockStyle.Fill;
            AnimationPanel.SetViewport(ActiveViewport.GetGLControl());
            dockContent.Controls.Add(AnimationPanel);
            LayoutTextureList.Show(dockPanel1, DockState.DockRight);

            if (ActiveViewport != null)
                dockContent.Show(ActiveViewport.Pane, DockAlignment.Bottom, 0.2);
            else
                dockContent.Show(dockPanel1, DockState.DockBottom);
        }

        private void stComboBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private bool isBGUpdating = false;
        private void viewportBackColorCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ActiveViewport == null || isBGUpdating) return;
            UpdateBackColor();
        }

        private void UpdateBackColor()
        {
            if (viewportBackColorCB.SelectedIndex == 0)
            {
                ActiveViewport.UpdateBackgroundColor(Color.FromArgb(130, 130, 130));
                backColorDisplay.BackColor = Color.FromArgb(130, 130, 130);
            }
            else if (!isLoaded)
            {
                ActiveViewport.UpdateBackgroundColor(Runtime.LayoutEditor.BackgroundColor);
                backColorDisplay.BackColor = Runtime.LayoutEditor.BackgroundColor;
            }
            else
            {
                ColorDialog dlg = new ColorDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ActiveViewport.UpdateBackgroundColor(dlg.Color);
                    backColorDisplay.BackColor = dlg.Color;
                }
                else
                    viewportBackColorCB.SelectedIndex = 0;
            }
        }

        private void backColorDisplay_Click(object sender, EventArgs e)
        {
            isBGUpdating = true;

            ColorDialog dlg = new ColorDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ActiveViewport.UpdateBackgroundColor(dlg.Color);
                backColorDisplay.BackColor = dlg.Color;

                if (viewportBackColorCB.SelectedIndex == 0)
                    viewportBackColorCB.SelectedIndex = 1;
            }

            isBGUpdating = false;
        }

        private void dockPanel1_ActiveDocumentChanged(object sender, EventArgs e)
        {
            var dockContent = dockPanel1.ActiveDocument as LayoutViewer;
            if (dockContent != null)
            {
                var file = (dockContent).LayoutFile;
                ReloadEditors(file);
                ActiveViewport = dockContent;

                dockContent.UpdateViewport();
            }
        }

        private void LayoutEditor_DragDrop(object sender, DragEventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string filename in files)
                OpenFile(filename);

            Cursor.Current = Cursors.Default;
        }

        private void OpenFile(string fileName)
        {
            //Todo if an image is dropped, we should make a picture pane if a viewer is active

            var file = STFileLoader.OpenFileFormat(fileName);
            if (file == null) return;

            if (file is BFLYT)
                LoadBxlyt(((BFLYT)file).header);
            else if (file is BCLYT)
                LoadBxlyt(((BCLYT)file).header);
            else if (file is BRLYT)
                LoadBxlyt(((BRLYT)file).header);
            else if (file is IArchiveFile)
            {
                var layouts = SearchLayoutFiles((IArchiveFile)file);
                if (layouts.Count > 1)
                {
                    var form = new FileSelector();
                    form.LoadLayoutFiles(layouts);
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        foreach (var layout in form.SelectedLayouts())
                        {
                            if (layout is BFLYT)
                                LoadBxlyt(((BFLYT)layout).header);
                            if (layout is BCLYT)
                                LoadBxlyt(((BCLYT)layout).header);
                            if (layout is BRLYT)
                                LoadBxlyt(((BRLYT)layout).header);
                        }
                    }
                }
                else if (layouts.Count > 0)
                {
                    if (layouts[0] is BFLYT)
                        LoadBxlyt(((BFLYT)layouts[0]).header);
                    if (layouts[0] is BCLYT)
                        LoadBxlyt(((BCLYT)layouts[0]).header);
                    if (layouts[0] is BRLYT)
                        LoadBxlyt(((BRLYT)layouts[0]).header);
                }
            }
            else if (file is BFLAN)
            {

            }
            else if (file is BNTX)
            {

            }
        }

        private List<IFileFormat> SearchLayoutFiles(IArchiveFile archiveFile)
        {
            List<IFileFormat> layouts = new List<IFileFormat>();

            foreach (var file in archiveFile.Files)
            {
                var fileFormat = STFileLoader.OpenFileFormat(file.FileName,
                    new Type[] { typeof(BFLYT), typeof(BCLYT), typeof(BRLYT), typeof(SARC) }, file.FileData);

                if (fileFormat is BFLYT)
                {
                    fileFormat.IFileInfo.ArchiveParent = archiveFile;
                    layouts.Add(fileFormat);
                }
                else if (fileFormat is BCLYT)
                {
                    fileFormat.IFileInfo.ArchiveParent = archiveFile;
                    layouts.Add(fileFormat);
                }
                else if (Utils.GetExtension(file.FileName) == ".bntx")
                {

                }
                else if (Utils.GetExtension(file.FileName) == ".bflim")
                {

                }
                else if (fileFormat is SARC)
                {
                    fileFormat.IFileInfo.ArchiveParent = archiveFile;

                    if (fileFormat is IArchiveFile)
                        return SearchLayoutFiles((IArchiveFile)fileFormat);
                }
            }

            return layouts;
        }

        private void LayoutEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
            {
                String[] strGetFormats = e.Data.GetFormats();
                e.Effect = DragDropEffects.None;
            }
        }

        private void clearWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var docs = dockPanel1.DocumentsToArray();
            for (int i = 0; i < docs.Length; i++)
                docs[i].DockHandler.DockPanel = null;

            ResetEditors();

            GC.Collect();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (string filename in ofd.FileNames)
                    OpenFile(filename);
            }
        }

        private void textConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!textConverterToolStripMenuItem.Checked)
            {
                if (ActiveLayout.FileInfo is BFLYT)
                {
                    if (TextConverter == null)
                        TextConverter = new LayoutTextDocked();
                    TextConverter.Text = "Text Converter";
                    TextConverter.TextCompiled += OnTextCompiled;
                    TextConverter.LoadLayout((BFLYT)ActiveLayout.FileInfo);
                    if (ActiveViewport != null)
                        TextConverter.Show(ActiveViewport.Pane, DockAlignment.Bottom, 0.4);
                    else
                        TextConverter.Show(dockPanel1, DockState.DockLeft);
                }

                textConverterToolStripMenuItem.Checked = true;
            }
            else
            {
                textConverterToolStripMenuItem.Checked = false;
                TextConverter?.Hide();
            }
        }

        private void OnTextCompiled(object sender, EventArgs e)
        {
            var layout = TextConverter.GetLayout();
            ActiveLayout = layout.header;
            ReloadEditors(layout.header);

            if (ActiveViewport != null)
                ActiveViewport.ResetLayout(ActiveLayout);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ActiveAnimation != null && ActiveAnimation.FileInfo.CanSave)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = Utils.GetAllFilters(ActiveAnimation.FileInfo);
                sfd.FileName = ActiveAnimation.FileInfo.FileName;

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    STFileSaver.SaveFileFormat(ActiveAnimation.FileInfo, sfd.FileName);
                }
            }

            if (ActiveLayout != null && ActiveLayout.FileInfo.CanSave)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = Utils.GetAllFilters(ActiveLayout.FileInfo);
                sfd.FileName = ActiveLayout.FileInfo.FileName;

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    STFileSaver.SaveFileFormat(ActiveLayout.FileInfo, sfd.FileName);
                }
            }
        }

        private void saveToolStripMenuItem1_Click(object sender, EventArgs e) {
            if (ActiveLayout != null)
                SaveActiveFile(ActiveLayout.FileInfo);
        }

        private void saveAnimationToolStripMenuItem_Click(object sender, EventArgs e) {
            if (ActiveAnimation != null)
                SaveActiveFile(ActiveAnimation.FileInfo);
        }

        private void SaveActiveFile(IFileFormat fileFormat, bool ForceDialog = false)
        {
            if (fileFormat.CanSave)
            {
                if (fileFormat.IFileInfo != null &&
                    fileFormat.IFileInfo.ArchiveParent != null && !ForceDialog)
                {
                    if (fileFormat is IEditorFormParameters)
                        ((IEditorFormParameters)fileFormat).OnSave.Invoke(fileFormat, new EventArgs());

                    MessageBox.Show($"Saved {fileFormat.FileName} to archive!");
                }
                else
                {
                    STFileSaver.SaveFileFormat(fileFormat, fileFormat.FilePath);
                }
            }
        }

        private void LayoutEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Alt && e.KeyCode == Keys.S) // Ctrl + Alt + S Save As
            {
                e.SuppressKeyPress = true;
                if (ActiveLayout != null)
                    SaveActiveFile(ActiveLayout.FileInfo, true);
                if (ActiveAnimation != null)
                    SaveActiveFile(ActiveAnimation.FileInfo, true);
            }
            else if (e.Control && e.KeyCode == Keys.S) // Ctrl + S Save
            {
                e.SuppressKeyPress = true;
                if (ActiveLayout != null)
                    SaveActiveFile(ActiveLayout.FileInfo, false);
                if (ActiveAnimation != null)
                    SaveActiveFile(ActiveAnimation.FileInfo, false);
            }
        }

        private void debugShading_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (debugShading.SelectedIndex < 0) return;

            Runtime.LayoutEditor.Shading = (Runtime.LayoutEditor.DebugShading)debugShading.SelectedItem;
            if (ActiveViewport != null)
                ActiveViewport.UpdateViewport();
        }

        private void orthographicViewToolStripMenuItem_Click(object sender, EventArgs e) {
            ToggleOrthMode();
        }

        private void toolstripOrthoBtn_Click(object sender, EventArgs e) {
            if (orthographicViewToolStripMenuItem.Checked)
                orthographicViewToolStripMenuItem.Checked = false;
            else
                orthographicViewToolStripMenuItem.Checked = true;

            ToggleOrthMode();
        }

        private void ToggleOrthMode()
        {
            if (ActiveViewport != null)
            {
                if (!orthographicViewToolStripMenuItem.Checked)
                    toolstripOrthoBtn.Image = BitmapExtension.GrayScale(FirstPlugin.Properties.Resources.OrthoView);
                else
                    toolstripOrthoBtn.Image = FirstPlugin.Properties.Resources.OrthoView;

                Runtime.LayoutEditor.UseOrthographicView = orthographicViewToolStripMenuItem.Checked; 
                ActiveViewport.ResetCamera();
                ActiveViewport.UpdateViewport();
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e) {
            if (ActiveLayout != null)
                SaveActiveFile(ActiveLayout.FileInfo, false);
            if (ActiveAnimation != null)
                SaveActiveFile(ActiveAnimation.FileInfo, false);
        }

        private void displayPanesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Runtime.LayoutEditor.DisplayNullPane = displayNullPanesToolStripMenuItem.Checked;
            Runtime.LayoutEditor.DisplayBoundryPane = displayyBoundryPanesToolStripMenuItem.Checked;
            Runtime.LayoutEditor.DisplayPicturePane = displayPicturePanesToolStripMenuItem.Checked;
            Runtime.LayoutEditor.DisplayWindowPane = displayWindowPanesToolStripMenuItem.Checked;
            Runtime.LayoutEditor.DisplayTextPane = displayTextPanesToolStripMenuItem.Checked;

            ActiveViewport?.UpdateViewport();
        }

        private void renderInGamePreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Runtime.LayoutEditor.IsGamePreview = renderInGamePreviewToolStripMenuItem.Checked;
            ActiveViewport?.UpdateViewport();
        }

        private void displayGridToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Runtime.LayoutEditor.DisplayGrid = displayGridToolStripMenuItem.Checked;
            ActiveViewport?.UpdateViewport();
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AnimationPanel?.Reset();
            ActiveViewport?.UpdateViewport();
        }

        private void showGameWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (GamePreviewWindow == null || GamePreviewWindow.Disposing || GamePreviewWindow.IsDisposed)
            {
                GamePreviewWindow = new LayoutViewer(this,ActiveLayout, Textures);
                GamePreviewWindow.GameWindow = true;
                GamePreviewWindow.Dock = DockStyle.Fill;
                STForm form = new STForm();
                form.Text = "Game Preview";
                form.AddControl(GamePreviewWindow);
                form.Show();
            }
        }

        private void LayoutEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            GamePreviewWindow?.OnControlClosing();
            GamePreviewWindow?.Dispose();
        }
    }
}

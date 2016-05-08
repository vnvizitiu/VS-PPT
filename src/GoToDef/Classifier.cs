﻿using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace GoToDef
{
    #region Classification type/format exports

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "UnderlineClassificationProPack")]
    [Name("UnderlineClassificationFormatProPack")]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class UnderlineFormatDefinition : ClassificationFormatDefinition
    {
        public UnderlineFormatDefinition()
        {
            this.DisplayName = "Underline";
            this.TextDecorations = System.Windows.TextDecorations.Underline;
            this.ForegroundColor = Colors.Blue;
        }
    }

    #endregion

    #region Provider definition

    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(ClassificationTag))]
    internal class UnderlineClassifierProvider : IViewTaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry;

        [Import]
        private SVsServiceProvider _serviceProvider;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("UnderlineClassificationProPack")]
        internal static ClassificationTypeDefinition underlineClassificationType;

        private static IClassificationType s_underlineClassification;
        public static UnderlineClassifier GetClassifierForView(ITextView view)
        {
            if (s_underlineClassification == null)
                return null;

            return view.Properties.GetOrCreateSingletonProperty(() => new UnderlineClassifier(view, s_underlineClassification));
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (s_underlineClassification == null)
                s_underlineClassification = ClassificationRegistry.GetClassificationType("UnderlineClassificationProPack");

            if (textView.TextBuffer != buffer)
                return null;

            IVsExtensionManager manager = _serviceProvider.GetService(typeof(SVsExtensionManager)) as IVsExtensionManager;
            if (manager == null)
                return null;

            IInstalledExtension extension;
            manager.TryGetInstalledExtension("GoToDef", out extension);
            if (extension != null)
                return null;

            return GetClassifierForView(textView) as ITagger<T>;
        }
    }

    #endregion

    internal class UnderlineClassifier : ITagger<ClassificationTag>
    {
        private IClassificationType _classificationType;
        private ITextView _textView;
        private SnapshotSpan? _underlineSpan;

        internal UnderlineClassifier(ITextView textView, IClassificationType classificationType)
        {
            _textView = textView;
            _classificationType = classificationType;
            _underlineSpan = null;
        }

        #region Private helpers

        private void SendEvent(SnapshotSpan span)
        {
            var temp = this.TagsChanged;
            if (temp != null)
                temp(this, new SnapshotSpanEventArgs(span));
        }

        #endregion

        #region UnderlineClassification public members

        public SnapshotSpan? CurrentUnderlineSpan { get { return _underlineSpan; } }

        public void SetUnderlineSpan(SnapshotSpan? span)
        {
            var oldSpan = _underlineSpan;
            _underlineSpan = span;

            if (!oldSpan.HasValue && !_underlineSpan.HasValue)
                return;

            else if (oldSpan.HasValue && _underlineSpan.HasValue && oldSpan == _underlineSpan)
                return;

            if (!_underlineSpan.HasValue)
            {
                this.SendEvent(oldSpan.Value);
            }
            else
            {
                SnapshotSpan updateSpan = _underlineSpan.Value;
                if (oldSpan.HasValue)
                    updateSpan = new SnapshotSpan(updateSpan.Snapshot,
                        Span.FromBounds(Math.Min(updateSpan.Start, oldSpan.Value.Start),
                                        Math.Max(updateSpan.End, oldSpan.Value.End)));

                this.SendEvent(updateSpan);
            }
        }

        #endregion

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!_underlineSpan.HasValue || spans.Count == 0)
                yield break;

            SnapshotSpan request = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End);
            SnapshotSpan underline = _underlineSpan.Value.TranslateTo(request.Snapshot, SpanTrackingMode.EdgeInclusive);
            if (underline.IntersectsWith(request))
            {
                yield return new TagSpan<ClassificationTag>(underline, new ClassificationTag(_classificationType));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
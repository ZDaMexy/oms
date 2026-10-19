// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneSkinBlueprintLifetime : OsuTestScene
    {
        // Only the editor's real selection authority is needed; do not start a draft or touch skin storage.
        [Cached]
        private readonly SkinEditor editor = new SkinEditor();

        [SetUpSteps]
        public void SetUpSteps() => AddStep("clear editor selection", () => editor.SelectedComponents.Clear());

        [Test]
        public void TestComponentChangesAndBidirectionalSelection()
        {
            ComponentTarget target = null!;
            BlueprintProbe blueprints = null!;
            var first = new Component();
            var second = new Component();
            var replacement = new Component();

            AddStep("mount target with an existing component", () =>
            {
                Add(target = new ComponentTarget());
                target.AddComponent(first);
                Add(blueprints = new BlueprintProbe(target));
            });
            AddUntilStep("existing component has a loaded blueprint", () =>
                blueprints.IsLoaded && blueprints.BlueprintItems.SequenceEqual(new[] { first }) && blueprints.AllBlueprintsLoaded);
            AddStep("publish another component", () => target.AddComponent(second));
            AddUntilStep("both components have loaded blueprints", () =>
                blueprints.BlueprintItems.Length == 2 && blueprints.AllBlueprintsLoaded);
            AddStep("editor selection selects the matching blueprint", () =>
            {
                editor.SelectedComponents.Add(first);
                Assert.That(blueprints.SelectedComponents, Is.EqualTo(new[] { first }));
                Assert.That(blueprints.SelectionHandler.SelectedBlueprints.Single().Item, Is.SameAs(first));
            });
            AddStep("blueprint selection updates the editor", () =>
            {
                editor.SelectedComponents.Clear();
                blueprints.SelectComponent(second);
                Assert.That(editor.SelectedComponents, Is.EqualTo(new[] { second }));
            });
            AddStep("select all uses the current target components", () =>
            {
                blueprints.SelectAllComponents();
                Assert.That(editor.SelectedComponents, Is.EquivalentTo(new[] { first, second }));
            });
            AddStep("replace the first component", () => target.ReplaceComponent(0, replacement));
            AddUntilStep("replacement removes the old blueprint", () =>
                blueprints.BlueprintItems.Length == 2
                && blueprints.BlueprintItems.Contains(replacement)
                && !blueprints.BlueprintItems.Contains(first));
            AddStep("remove the second component", () => target.RemoveComponent(second, true));
            AddUntilStep("only the replacement remains", () =>
                blueprints.BlueprintItems.SequenceEqual(new[] { replacement }));
            AddStep("clear components", () => target.ClearComponents());
            AddUntilStep("all blueprints and selections removed", () =>
                blueprints.BlueprintItems.Length == 0 && editor.SelectedComponents.Count == 0);
        }

        [Test]
        public void TestBackgroundDisposalDoesNotAcquireThePublisherBindingLock()
        {
            ComponentTarget target = null!;
            BlueprintProbe blueprints = null!;
            BindableList<ISerialisableDrawable> independentObserver = null!;

            AddStep("mount a target with an independent binding", () =>
            {
                Add(target = new ComponentTarget());
                independentObserver = target.MutableComponents.GetBoundCopy();
                Add(blueprints = new BlueprintProbe(target));
            });
            AddUntilStep("blueprints loaded", () => blueprints.IsLoaded);
            AddStep("dispose while publication owns its original binding lock", () =>
            {
                Assert.That(Remove(blueprints, disposeImmediately: false), Is.True);

                object bindings = typeof(BindableList<ISerialisableDrawable>)
                                  .GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic)!
                                  .GetValue(target.MutableComponents)!;
                object publisherLock = bindings.GetType().GetField("list", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bindings)!;

                using var lockEntered = new ManualResetEventSlim();
                using var releaseLock = new ManualResetEventSlim();
                using var disposalEntered = new ManualResetEventSlim();
                blueprints.DisposalStarted = disposalEntered.Set;

                Task lockHolder = Task.Run(() =>
                {
                    lock (publisherLock)
                    {
                        lockEntered.Set();
                        if (!releaseLock.Wait(TimeSpan.FromSeconds(20)))
                            throw new TimeoutException("The test did not release the publisher binding lock.");
                    }
                });
                Task? disposal = null;
                bool completedWhilePublisherLocked = false;

                try
                {
                    Assert.That(lockEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    disposal = Task.Run(blueprints.Dispose);
                    Assert.That(disposalEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    completedWhilePublisherLocked = disposal.Wait(TimeSpan.FromSeconds(2));
                }
                finally
                {
                    // Never strand either worker, even when checking the old implementation.
                    releaseLock.Set();
                    Assert.That(Task.WaitAll(disposal == null ? new[] { lockHolder } : new[] { lockHolder, disposal }, TimeSpan.FromSeconds(5)), Is.True);
                    blueprints.DisposalStarted = null;
                }

                Assert.That(completedWhilePublisherLocked, Is.True,
                    "Observing components must not add a reverse binding edge from background blueprint disposal to the publishing list.");

                var laterComponent = new Component();
                target.AddComponent(laterComponent);
                Assert.That(independentObserver, Is.EqualTo(new[] { laterComponent }),
                    "Disposing blueprints must not unbind the publisher or its other observers.");
                independentObserver.UnbindAll();
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestComponentNotificationsCannotCreateBlueprintsAfterDisposal(bool queuedBeforeDisposal)
        {
            ComponentTarget target = null!;
            BlueprintProbe blueprints = null!;

            AddStep("mount an observing blueprint container", () =>
            {
                Add(target = new ComponentTarget());
                Add(blueprints = new BlueprintProbe(target) { RecordAddAttemptsOnly = true });
            });
            AddUntilStep("blueprints loaded", () => blueprints.IsLoaded);
            AddStep("complete queued and already captured notifications after disposal", () =>
            {
                Assert.That(Remove(blueprints, disposeImmediately: false), Is.True);
                var component = new Component();

                // An event publisher may already have captured this handler when unsubscription happens.
                var capturedNotification = typeof(SkinBlueprintContainer)
                                           .GetMethod("componentsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                                           .CreateDelegate<NotifyCollectionChangedEventHandler>(blueprints);

                if (queuedBeforeDisposal)
                    target.AddComponent(component);

                blueprints.Dispose();

                if (!queuedBeforeDisposal)
                    capturedNotification(target.MutableComponents, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, component));

                blueprints.RunPendingCallbacks();
                Assert.That(blueprints.AddAttempts, Is.Zero,
                    "A callback captured or queued before teardown must not resurrect blueprint content.");
            });
        }

        private sealed partial class BlueprintProbe : SkinBlueprintContainer
        {
            public Action? DisposalStarted;
            public bool RecordAddAttemptsOnly;
            public int AddAttempts;

            public ISerialisableDrawable[] BlueprintItems => SelectionBlueprints.Select(blueprint => blueprint.Item).ToArray();
            public bool AllBlueprintsLoaded => SelectionBlueprints.All(blueprint => blueprint.IsLoaded);
            public ISerialisableDrawable[] SelectedComponents => SelectedItems.ToArray();

            public BlueprintProbe(ISerialisableDrawableContainer target)
                : base(target)
            {
            }

            public void SelectComponent(ISerialisableDrawable component) => GetBlueprintFor(component).Select();
            public void SelectAllComponents() => SelectAll();
            public void RunPendingCallbacks() => Scheduler.Update();

            protected override void AddBlueprintFor(ISerialisableDrawable item)
            {
                AddAttempts++;
                if (!RecordAddAttemptsOnly)
                    base.AddBlueprintFor(item);
            }

            protected override void Dispose(bool isDisposing)
            {
                DisposalStarted?.Invoke();
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class ComponentTarget : Container, ISerialisableDrawableContainer
        {
            public readonly BindableList<ISerialisableDrawable> MutableComponents = new BindableList<ISerialisableDrawable>();
            public IBindableList<ISerialisableDrawable> Components => MutableComponents;

            public ComponentTarget()
            {
                RelativeSizeAxes = Axes.Both;
            }

            public void AddComponent(ISerialisableDrawable component)
            {
                Add((Drawable)component);
                MutableComponents.Add(component);
            }

            public void RemoveComponent(ISerialisableDrawable component, bool disposeImmediately)
            {
                MutableComponents.Remove(component);
                Remove((Drawable)component, disposeImmediately);
            }

            public void ReplaceComponent(int index, ISerialisableDrawable replacement)
            {
                ISerialisableDrawable previous = MutableComponents[index];
                Add((Drawable)replacement);
                MutableComponents[index] = replacement;
                Remove((Drawable)previous, disposeImmediately: true);
            }

            public void ClearComponents()
            {
                MutableComponents.Clear();
                Clear(disposeChildren: true);
            }

            public void Reload() => ClearComponents();
            void ISerialisableDrawableContainer.Add(ISerialisableDrawable drawable) => AddComponent(drawable);
            void ISerialisableDrawableContainer.Remove(ISerialisableDrawable component, bool disposeImmediately) => RemoveComponent(component, disposeImmediately);
        }

        private sealed partial class Component : Box, ISerialisableDrawable
        {
            public bool UsesFixedAnchor { get; set; }

            public Component()
            {
                Width = 64;
                Height = 32;
            }
        }
    }
}

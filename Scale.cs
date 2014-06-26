using KSPAPIExtensions;
/* GoodspeedTweakScale plugin (c) Copyright 2014 Gaius Goodspeed

This software is made available by the author under the terms of the
Creative Commons Attribution-NonCommercial-ShareAlike license.  See
the following web page for details:

http://creativecommons.org/licenses/by-nc-sa/4.0/

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TweakScale
{
    /// <summary>
    /// Converts from Gaius' GoodspeedTweakScale to updated TweakScale.
    /// </summary>
    public class GoodspeedTweakScale : TweakScale
    {
        private bool updated = false;

        protected override void Setup()
        {
            base.Setup();
            if (!updated)
            {
                tweakName = (int)tweakScale;
                tweakScale = scaleFactors[tweakName];
            }
        }
    }
    public class TweakScale : PartModule
    {
        /// <summary>
        /// The selected scale. Different from currentScale only for a single update, where currentScale is set to match this.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scale", guiFormat = "S4", guiUnits = "m")]
        [UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0.625f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float tweakScale = 1;

        /// <summary>
        /// Index into scale values array.
        /// </summary>
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Scale")]
        [UI_ChooseOption(scene = UI_Scene.Editor)]
        public int tweakName = 0;

        /// <summary>
        /// The scale to which the part currently is scaled.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentScale = -1;

        /// <summary>
        /// The default scale, i.e. the number by which to divide tweakScale and currentScale to get the relative size difference from when the part is used without TweakScale.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float defaultScale = -1;
        
        /// <summary>
        /// The default index into scale values array. Mostly same as above.
        /// </summary>
        [KSPField(isPersistant = true)]
        public int defaultName = -1;

        /// <summary>
        /// Whether the part should be freely scalable or limited to a list of allowed values.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool isFreeScale = false;

        /// <summary>
        /// The version of TweakScale last used to change this part. Intended for use in the case of non-backward-compatible changes.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string version;

        /// <summary>
        /// The scale value array. If isFreeScale is false, the part may only be one of these scales.
        /// </summary>
        protected float[] scaleFactors = { 0.625f, 1.25f, 2.5f, 3.75f, 5f };

        /// <summary>
        /// The unmodified prefab part. From this, default values are found.
        /// </summary>
        private Part prefabPart;

        /// <summary>
        /// Like currentScale above, this is the current scale vector. If TweakScale supports non-uniform scaling in the future (e.g. changing only the length of a booster), savedScale may represent such a scaling, while currentScale won't.
        /// </summary>
        private Vector3 savedScale;

        /// <summary>
        /// The value by which the part is scaled by default. When a part uses MODEL { scale = ... }, this will be different from (1,1,1).
        /// </summary>
        [KSPField(isPersistant = true)]
        public Vector3 defaultTransformScale = new Vector3(0f, 0f, 0f);

        /// <summary>
        /// Updaters for different PartModules.
        /// </summary>
        private IRescalable[] updaters;

        /// <summary>
        /// Whether this instance of TweakScale is the first. If not, log an error and make sure the TweakScale modules don't harmfully interact.
        /// </summary>
        private bool duplicate = false;

        /// <summary>
        /// The Config for this part.
        /// </summary>
        private ScaleConfig config;

        /// <summary>
        /// The ConfigNode that belongs to the part this module affects.
        /// </summary>
        private ConfigNode PartNode
        {
            get
            {
                return GameDatabase.Instance.GetConfigs("PART").Single(c => c.name.Replace('_', '.') == part.partInfo.name)
                    .config;
            }
        }

        /// <summary>
        /// The ConfigNode that belongs to this module.
        /// </summary>
        public ConfigNode moduleNode
        {
            get
            {
                return PartNode.GetNodes("MODULE").FirstOrDefault(n => n.GetValue("name") == moduleName);
            }
        }

        /// <summary>
        /// The current scaling factor.
        /// </summary>
        private ScalingFactor scalingFactor
        {
            get
            {
                return new ScalingFactor(tweakScale / defaultScale, tweakScale / currentScale, isFreeScale ? -1 : tweakName);
            }
        }

        /// <summary>
        /// The smallest scale the part can be.
        /// </summary>
        private float minSize
        {
            get
            {
                if (isFreeScale)
                {
                    var range = (UI_FloatEdit)this.Fields["tweakScale"].uiControlEditor;
                    return range.minValue;
                }
                else
                {
                    return scaleFactors.Min();
                }
            }
        }

        /// <summary>
        /// The largest scale the part can be.
        /// </summary>
        private float maxSize
        {
            get
            {
                if (isFreeScale)
                {
                    var range = (UI_FloatEdit)this.Fields["tweakScale"].uiControlEditor;
                    return range.maxValue;
                }
                else
                {
                    return scaleFactors.Max();
                }
            }
        }

        /// <summary>
        /// Loads settings from <paramref name="config"/>.
        /// </summary>
        /// <param name="config">The settings to use.</param>
        private void SetupFromConfig(ScaleConfig config)
        {
            isFreeScale = config.isFreeScale;
            defaultScale = config.defaultScale;
            this.Fields["tweakScale"].guiActiveEditor = false;
            this.Fields["tweakName"].guiActiveEditor = false;
            if (isFreeScale)
            {
                this.Fields["tweakScale"].guiActiveEditor = true;
                var range = (UI_FloatEdit)this.Fields["tweakScale"].uiControlEditor;
                range.minValue = config.minValue;
                range.maxValue = config.maxValue;
                range.incrementLarge = (float)Math.Round((range.maxValue - range.minValue) / 10, 2);
                range.incrementSmall = (float)Math.Round(range.incrementLarge / 10, 2);
                this.Fields["tweakScale"].guiUnits = config.suffix;
            }
            else if (config.scaleFactors.Length > 1)
            {
                this.Fields["tweakName"].guiActiveEditor = true;
                var options = (UI_ChooseOption)this.Fields["tweakName"].uiControlEditor;
                scaleFactors = config.scaleFactors;
                options.options = config.scaleNames;
            }
        }

        /// <summary>
        /// Sets up values from config, creates updaters, and sets up initial values.
        /// </summary>
        protected virtual void Setup()
        {
            if (part.partInfo == null)
            {
                return;
            }
            prefabPart = PartLoader.getPartInfoByName(part.partInfo.name).partPrefab;

            updaters = TweakScaleUpdater.createUpdaters(part).ToArray();

            SetupFromConfig(config = new ScaleConfig(moduleNode));

            if (currentScale < 0f)
            {
                tweakScale = currentScale = defaultScale;
                if (!isFreeScale)
                {
                    tweakName = defaultName = Tools.ClosestIndex(defaultScale, scaleFactors);
                }
            }
            else
            {
                if (!isFreeScale)
                {
                    tweakName = defaultName = Tools.ClosestIndex(tweakScale, scaleFactors);
                }
                updateByWidth(scalingFactor, false);
            }

            foreach (var updater in updaters)
            {
                updater.OnRescale(scalingFactor);
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Setup();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Setup();
        }

        public override void OnSave(ConfigNode node)
        {
            version = this.GetType().Assembly.GetName().Version.ToString();
            base.OnSave(node);
        }

        /// <summary>
        /// Moves <paramref name="node"/> to reflect the new scale. If <paramref name="movePart"/> is true, also moves attached parts.
        /// </summary>
        /// <param name="node">The node to move.</param>
        /// <param name="baseNode">The same node, as found on the prefab part.</param>
        /// <param name="factor">The factor by which to modify the node's location.</param>
        /// <param name="movePart">Whether or not to move attached parts.</param>
        private void moveNode(AttachNode node, AttachNode baseNode, ScalingFactor factor, bool movePart)
        {
            Vector3 oldPosition = node.position;
            node.position = baseNode.position * factor.absolute.linear;
            if (movePart && node.attachedPart != null)
            {
                if (node.attachedPart == part.parent)
                    part.transform.Translate(oldPosition - node.position);
                else
                    node.attachedPart.transform.Translate(node.position - oldPosition, part.transform);
            }
            if (isFreeScale)
            {
                node.size = (int)(baseNode.size + (tweakScale - defaultScale) / (maxSize - minSize) * 5);
            }
            else
            {
                var options = (UI_ChooseOption)this.Fields["tweakName"].uiControlEditor;
                node.size = (int)(baseNode.size + (tweakName - defaultName) / (float)options.options.Length * 5);
            }
            if (node.size < 0)
            {
                node.size = 0;
            }
        }

        /// <summary>
        /// Updates properties that change linearly with scale.
        /// </summary>
        /// <param name="factor">The factor by which to modify properties</param>
        /// <param name="moveParts">Whether or not to move attached parts.</param>
        private void updateByWidth(ScalingFactor factor, bool moveParts)
        {
            if (defaultTransformScale.x == 0.0f)
            {
                defaultTransformScale = part.transform.GetChild(0).localScale;
            }

            savedScale = part.transform.GetChild(0).localScale = factor.absolute.linear * defaultTransformScale;
            part.transform.GetChild(0).hasChanged = true;
            part.transform.hasChanged = true;

            foreach (AttachNode node in part.attachNodes)
            {
                var nodesWithSameId = part.attachNodes
                    .Where(a => a.id == node.id)
                    .ToArray();
                var idIdx = Array.FindIndex(nodesWithSameId, a => a == node);
                var baseNodesWithSameId = prefabPart.attachNodes
                    .Where(a => a.id == node.id)
                    .ToArray();
                if (idIdx < baseNodesWithSameId.Length)
                {
                    var baseNode = baseNodesWithSameId[idIdx];

                    moveNode(node, baseNode, factor, moveParts);
                }
                else
                {
                    Tools.Logf("Error scaling part. Node {0} does not have counterpart in base part.", node.id);
                }
            }

            if (part.srfAttachNode != null)
            {
                moveNode(part.srfAttachNode, prefabPart.srfAttachNode, factor, moveParts);
            }
            if (moveParts)
            {
                foreach (Part child in part.children)
                {
                    if (child.srfAttachNode != null && child.srfAttachNode.attachedPart == part) // part is attached to us, but not on a node
                    {
                        Vector3 attachedPosition = child.transform.localPosition + child.transform.localRotation * child.srfAttachNode.position;
                        Vector3 targetPosition = attachedPosition * factor.absolute.linear;
                        child.transform.Translate(targetPosition - attachedPosition, part.transform);
                    }
                }
            };
        }

        /// <summary>
        /// Updates properties that change with the square of the scale. i.e. surface area.
        /// </summary>
        /// <param name="factor">The factor by which to modify properties</param>
        private void updateBySurfaceArea(ScalingFactor factor) // values that change relative to the surface area (i.e. scale squared)
        {
            if (prefabPart.breakingForce == 22f) // not defined in the config, set to a reasonable default
                part.breakingForce = 32.0f * factor.absolute.quadratic; // scale 1 = 50, scale 2 = 200, etc.
            else // is defined, scale it relative to new surface area
                part.breakingForce = prefabPart.breakingForce * factor.absolute.quadratic;
            if (part.breakingForce < 22f)
                part.breakingForce = 22f;

            if (prefabPart.breakingTorque == 22f)
                part.breakingTorque = 32.0f * factor.absolute.quadratic;
            else
                part.breakingTorque = prefabPart.breakingTorque * factor.absolute.quadratic;
            if (part.breakingTorque < 22f)
                part.breakingTorque = 22f;

            foreach (var node in part.attachNodes)
            {
                node.breakingForce = part.breakingForce;
                node.breakingTorque = part.breakingTorque;
            }
        }

        /// <summary>
        /// Updates properties that change with the cube of the scale, i.e. the volume.
        /// </summary>
        /// <param name="factor">The factor by which to modify properties</param>
        private void updateByVolume(ScalingFactor factor) // values that change relative to the volume (i.e. scale cubed)
        {
            var newResourceValues = part.Resources.OfType<PartResource>().Select(a => new[] { a.amount * factor.relative.cubic, a.maxAmount * factor.relative.cubic }).ToArray();

            int idx = 0;
            foreach (PartResource resource in part.Resources)
            {
                var newValues = newResourceValues[idx];
                resource.amount = newValues[0];
                resource.maxAmount = newValues[1];
                idx++;
            }
        }

        /// <summary>
        /// Whether the part holds any resources (fuel, electricity, etc).
        /// </summary>
        private bool hasResources
        {
            get
            {
                return part.Resources.Count > 0;
            }
        }

        /// <summary>
        /// Marks the right-click window as dirty (i.e. tells it to update).
        /// </summary>
        private void updateWindow() // redraw the right-click window with the updated stats
        {
            if (!isFreeScale && hasResources)
            {
                foreach (UIPartActionWindow win in FindObjectsOfType(typeof(UIPartActionWindow)))
                {
                    if (win.part == part)
                    {
                        // This causes the slider to be non-responsive - i.e. after you click once, you must click again, not drag the slider.
                        win.displayDirty = true;
                    }
                }
            }
        }

        public void Update()
        {
            if (duplicate)
            {
                return;
            }
            if (this != part.Modules.OfType<TweakScale>().First())
            {
                Tools.Logf("Duplicate TweakScale module on part [{0}] {1}", part.partInfo.name, part.partInfo.title);
                Fields["tweakScale"].guiActiveEditor = false;
                Fields["tweakName"].guiActiveEditor = false;
                duplicate = true;
                return;
            }
            if (HighLogic.LoadedSceneIsEditor && currentScale >= 0f)
            {
                bool changed = isFreeScale ? tweakScale != currentScale : currentScale != scaleFactors[tweakName];

                if (changed) // user has changed the scale tweakable
                {
                    if (!isFreeScale)
                    {
                        tweakScale = scaleFactors[tweakName];
                    }

                    updateByWidth(scalingFactor, true);
                    updateBySurfaceArea(scalingFactor);
                    updateByVolume(scalingFactor);
                    updateWindow();

                    currentScale = tweakScale;
                    foreach (var updater in updaters)
                    {
                        updater.OnRescale(scalingFactor);
                    }
                }
                else if (part.transform.GetChild(0).localScale != savedScale) // editor frequently nukes our OnStart resize some time later
                {
                    updateByWidth(scalingFactor, false);
                }
            }
        }
    }
}
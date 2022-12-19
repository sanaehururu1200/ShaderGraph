using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.Graphing;
using UnityEngine;
using UnityEditor.Graphing.Util;
using UnityEditor.Graphs.AnimationBlendTree;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Toggle = UnityEngine.UIElements.Toggle;

namespace UnityEditor.ShaderGraph.Drawing
{
    class MasterNodeSettingsView : VisualElement
    {
        private const string k_InvalidShaderGUI = "No class named {0} which derives from ShaderGUI was found in this project.";
        private const string k_ShaderGUIToolTip = "Provide a ShaderGUI class that will be used as the Material Inspector for Materials using this Shader Graph";

        private ICanChangeShaderGUI m_CanChangeShaderGUI;
        private AbstractMaterialNode m_MasterNode;

        private TextField m_ShaderGUITextField;
        private PropertyRow m_OverrideFieldRow;
        private PropertySheet m_PropertySheet;

        public MasterNodeSettingsView(AbstractMaterialNode node)
        {
            m_MasterNode = node;
            m_CanChangeShaderGUI = node as ICanChangeShaderGUI;
            if (m_CanChangeShaderGUI == null)
            {
                Debug.LogError("MasterNodeSettingsView should only used on Master Nodes that implement ICanChangeShaderGUI");
            }
        }

        protected PropertySheet GetShaderGUIOverridePropertySheet()
        {
            m_PropertySheet = new PropertySheet();
            
            /*m_PropertySheet.Add(new PropertyRow(new Label("Rendering Mode Override")), (row) =>
            {
                row.Add(new EnumField(RenderMode.Opaque), (field) =>
                {
                    field.value = m_MasterNode.renderModeOverride;
                    field.RegisterValueChangedCallback(ChangeRenderingMode);
                });
            });*/

            if (m_MasterNode is MasterNode masterNode)
            {
                
                m_PropertySheet.Add(new PropertyRow(new Label("Culling Override")), (row) =>
                {
                    row.Add(new EnumField(MasterNode.CullingOverrideMode.None), (field) =>
                    {
                        field.value = masterNode.cullingOverride;
                        field.RegisterValueChangedCallback((callback) =>
                        {
                            masterNode.cullingOverride = (MasterNode.CullingOverrideMode)callback.newValue;
                        });
                    });
                });
                
                m_PropertySheet.Add(new PropertyRow(new Label("Alpha To Coverage")), (row) =>
                {
                    var alphaToCoverage = new Toggle();
                    row.Add(alphaToCoverage, (toggle) =>
                    {
                        toggle.value = masterNode.alphaToCoverage;
                        toggle.OnToggleChanged((evt) =>
                        {
                            masterNode.alphaToCoverage = evt.newValue;
                        });
                    });
                });
                
                
                m_PropertySheet.Add(new PropertyRow(new Label("Additional Pass")), (row) =>
                {
                    row.Add(new ObjectField(), (shaderObject) =>
                    {
                        shaderObject.objectType = typeof(Shader);
                        shaderObject.value = masterNode.additionalPass;
                        shaderObject.RegisterValueChangedCallback((callback) =>
                        {
                            masterNode.additionalPass = (Shader)callback.newValue;
                        });
                    });
                });
            }
            

            Toggle enabledToggle = new Toggle();
            m_PropertySheet.Add(new PropertyRow(new Label("Override ShaderGUI")), (row) =>
            {
                enabledToggle = new Toggle();
                row.Add(enabledToggle, (toggle) =>
                {
                    toggle.value = m_CanChangeShaderGUI.OverrideEnabled;
                    toggle.OnToggleChanged(ChangeOverrideEnabled);
                });
            });

            m_OverrideFieldRow = new PropertyRow(new Label("ShaderGUI"));
            m_ShaderGUITextField = new TextField();
            m_OverrideFieldRow.Add(m_ShaderGUITextField, (text) =>
            {
                text.isDelayed = true;
                text.RegisterValueChangedCallback(ChangeShaderGUIOverride);
            });

            // Set up such that both fields have the correct values (if displayed) & spawn warning if needed
            ProcessOverrideEnabledToggle(m_CanChangeShaderGUI.OverrideEnabled);

            m_PropertySheet.tooltip = k_ShaderGUIToolTip;

            return m_PropertySheet;
        }

        private void ChangeOverrideEnabled(ChangeEvent<bool> evt)
        {
            m_MasterNode.owner.owner.RegisterCompleteObjectUndo("Override Enabled Change");
            ProcessOverrideEnabledToggle(evt.newValue);
        }

        private void ChangeShaderGUIOverride(ChangeEvent<string> evt)
        {
            ProcessShaderGUIField(evt.newValue, true);
        }


        void ChangeRenderingMode(ChangeEvent<Enum> evt)
        {
            if (Equals(m_MasterNode.renderModeOverride, evt.newValue))
                return;

            m_MasterNode.owner.owner.RegisterCompleteObjectUndo("Rendering Mode Change");
            m_MasterNode.renderModeOverride = (RenderMode)evt.newValue;
        }

        private void ProcessOverrideEnabledToggle(bool newValue)
        {
            string storedValue = m_CanChangeShaderGUI.ShaderGUIOverride;

            m_CanChangeShaderGUI.OverrideEnabled = newValue;

            // Display the ShaderGUI text field only when the override is enabled
            if (m_CanChangeShaderGUI.OverrideEnabled)
            {
                m_PropertySheet.Add(m_OverrideFieldRow);

                ProcessShaderGUIField(storedValue, false);
            }
            else if (m_PropertySheet.Contains(m_OverrideFieldRow))
            {
                m_PropertySheet.Remove(m_OverrideFieldRow);
            }

            AddWarningIfNeeded();
        }

        private  void ProcessShaderGUIField(string newValue, bool recordUndo)
        {
            if (newValue == null)
            {
                newValue = "";
            }

            string sanitizedInput = Regex.Replace(newValue, @"(?:[^A-Za-z0-9._])|(?:\s)", "");

            if (sanitizedInput != m_CanChangeShaderGUI.ShaderGUIOverride)
            {
                if (recordUndo)
                {
                    m_MasterNode.owner.owner.RegisterCompleteObjectUndo("ShaderGUI Change");
                }

                m_CanChangeShaderGUI.ShaderGUIOverride = sanitizedInput;
            }

            m_ShaderGUITextField.value = m_CanChangeShaderGUI.ShaderGUIOverride;

            AddWarningIfNeeded();
        }

        // Add a warning to the node if the ShaderGUI is not found by Unity.
        private void AddWarningIfNeeded()
        {
            if (m_CanChangeShaderGUI.OverrideEnabled && m_CanChangeShaderGUI.ShaderGUIOverride != null && !ValidCustomEditorType(m_CanChangeShaderGUI.ShaderGUIOverride))
            {
                m_MasterNode.owner.messageManager?.ClearNodesFromProvider(this, m_MasterNode.ToEnumerable());
                m_MasterNode.owner.messageManager?.AddOrAppendError(this, m_MasterNode.tempId,
                    new ShaderMessage(string.Format(k_InvalidShaderGUI, m_CanChangeShaderGUI.ShaderGUIOverride), ShaderCompilerMessageSeverity.Warning));
            }
            else
            {
                m_MasterNode.owner.messageManager?.ClearNodesFromProvider(this, m_MasterNode.ToEnumerable());
            }
        }

        // Matches what trunk does to extract CustomEditors (Editor/Mono/Inspector/ShaderGUI.cs: ExtractCustomEditorType)
        private bool ValidCustomEditorType(string customEditorName)
        {
            if (string.IsNullOrEmpty(customEditorName))
            {
                return true; // No default, so this is valid.
            }

            var unityEditorFullName = $"UnityEditor.{customEditorName}"; // For convenience: adding UnityEditor namespace is not needed in the shader
            foreach (var type in TypeCache.GetTypesDerivedFrom<ShaderGUI>())
            {
                if (type.FullName.Equals(customEditorName, StringComparison.Ordinal) || type.FullName.Equals(unityEditorFullName, StringComparison.Ordinal))
                {
                    return typeof(ShaderGUI).IsAssignableFrom(type);
                }
            }
            return false;
        }
    }

}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using MESHBUILDER = SharpGLTF.Geometry.IMeshBuilder<SharpGLTF.Materials.MaterialBuilder>;

namespace SharpGLTF.Scenes
{
    /// <summary>
    /// Wraps a content object (usually a Mesh, a Camera or a light)
    /// </summary>
    public abstract class ContentTransformer
    {
        #region lifecycle

        protected ContentTransformer(Object content)
        {
            Guard.NotNull(content, nameof(content));

            _Content = content;
        }

        protected ContentTransformer(MESHBUILDER mesh)
        {
            Guard.NotNull(mesh, nameof(mesh));

            _Content = new MeshContent(mesh);
        }

        public abstract ContentTransformer Clone();

        protected ContentTransformer(ContentTransformer other)
        {
            this._Content = other._Content;
            this._Morphings = other._Morphings?.Clone();
        }

        #endregion

        #region data

        private Object _Content;

        private Animations.AnimatableProperty<Transforms.SparseWeight8> _Morphings; // maybe it should be moved to transformers!!

        #endregion

        #region properties

        public Object Content => _Content;

        public Animations.AnimatableProperty<Transforms.SparseWeight8> Morphings => _Morphings;

        #endregion

        #region API

        public virtual MESHBUILDER GetGeometryAsset() { return (_Content as IRenderableContent)?.GetGeometryAsset(); }

        public abstract NodeBuilder GetArmatureAsset();

        public Animations.AnimatableProperty<Transforms.SparseWeight8> UseMorphing()
        {
            if (_Morphings == null)
            {
                _Morphings = new Animations.AnimatableProperty<Transforms.SparseWeight8>();
                _Morphings.Value = default;
            }

            return _Morphings;
        }

        public Animations.CurveBuilder<Transforms.SparseWeight8> UseMorphing(string animationTrack)
        {
            return UseMorphing().UseTrackBuilder(animationTrack);
        }

        #endregion
    }

    /// <summary>
    /// Applies a static transform to the underlaying content.
    /// </summary>
    public partial class StaticTransformer : ContentTransformer
    {
        #region lifecycle

        public StaticTransformer(Object content, Matrix4x4 xform)
            : base(content)
        {
            _WorldTransform = xform;
        }

        public StaticTransformer(MESHBUILDER mesh, Matrix4x4 xform)
            : base(mesh)
        {
            _WorldTransform = xform;
        }

        protected StaticTransformer(StaticTransformer other) : base(other)
        {
            this._WorldTransform = other._WorldTransform;
        }

        public override ContentTransformer Clone()
        {
            return new StaticTransformer(this);
        }

        #endregion

        #region data

        private Matrix4x4 _WorldTransform;

        #endregion

        #region properties

        public Matrix4x4 WorldTransform
        {
            get => _WorldTransform;
            set => _WorldTransform = value;
        }

        #endregion

        #region API

        public override NodeBuilder GetArmatureAsset() { return null; }

        #endregion
    }

    /// <summary>
    /// Applies the transform of a <see cref="NodeBuilder"/> to the underlaying content.
    /// </summary>
    public partial class NodeTransformer : ContentTransformer
    {
        #region lifecycle

        public NodeTransformer(Object content, NodeBuilder node)
            : base(content)
        {
            _Node = node;
        }

        public NodeTransformer(MESHBUILDER mesh, NodeBuilder node)
            : base(mesh)
        {
            _Node = node;
        }

        protected NodeTransformer(NodeTransformer other) : base(other)
        {
            this._Node = other._Node;
        }

        public override ContentTransformer Clone()
        {
            return new NodeTransformer(this);
        }

        #endregion

        #region data

        private NodeBuilder _Node;

        #endregion

        #region properties

        public NodeBuilder Transform
        {
            get => _Node;
            set => _Node = value;
        }

        #endregion

        #region API

        public override NodeBuilder GetArmatureAsset() { return _Node.Root; }

        #endregion
    }

    /// <summary>
    /// Applies the transforms of many <see cref="NodeBuilder"/> to the underlaying content.
    /// </summary>
    public partial class SkinTransformer : ContentTransformer
    {
        #region lifecycle

        public SkinTransformer(MESHBUILDER mesh, Matrix4x4 meshWorldMatrix, NodeBuilder[] joints)
            : base(mesh)
        {
            SetJoints(meshWorldMatrix, joints);
        }

        public SkinTransformer(MESHBUILDER mesh, (NodeBuilder Joint, Matrix4x4 InverseBindMatrix)[] joints)
            : base(mesh)
        {
            SetJoints(joints);
        }

        protected SkinTransformer(SkinTransformer other)
            : base(other)
        {
            this._TargetBindMatrix = other._TargetBindMatrix;
            this._Joints.AddRange(other._Joints);
        }

        public override ContentTransformer Clone()
        {
            return new SkinTransformer(this);
        }

        #endregion

        #region data

        private Matrix4x4? _TargetBindMatrix;

        // condition: all NodeBuilder objects must have the same root.
        private readonly List<(NodeBuilder Joints, Matrix4x4? InverseBindMatrix)> _Joints = new List<(NodeBuilder, Matrix4x4?)>();

        #endregion

        #region API

        private void SetJoints(Matrix4x4 meshWorldMatrix, NodeBuilder[] joints)
        {
            Guard.NotNull(joints, nameof(joints));
            Guard.IsTrue(NodeBuilder.IsValidArmature(joints), nameof(joints));

            _TargetBindMatrix = meshWorldMatrix;
            _Joints.Clear();
            _Joints.AddRange(joints.Select(item => (item, (Matrix4x4?)null)));
        }

        private void SetJoints((NodeBuilder Joint, Matrix4x4 InverseBindMatrix)[] joints)
        {
            Guard.NotNull(joints, nameof(joints));
            Guard.IsTrue(NodeBuilder.IsValidArmature(joints.Select(item => item.Joint)), nameof(joints));

            _TargetBindMatrix = null;
            _Joints.Clear();
            _Joints.AddRange(joints.Select(item => (item.Joint, (Matrix4x4?)item.InverseBindMatrix)));
        }

        public (NodeBuilder Joint, Matrix4x4 InverseBindMatrix)[] GetJointBindings()
        {
            var jb = new (NodeBuilder Joint, Matrix4x4 InverseBindMatrix)[_Joints.Count];

            for (int i = 0; i < jb.Length; ++i)
            {
                var j = _Joints[i].Joints;
                var m = _Joints[i].InverseBindMatrix ?? Transforms.SkinTransform.CalculateInverseBinding(_TargetBindMatrix ?? Matrix4x4.Identity, j.WorldMatrix);

                jb[i] = (j, m);
            }

            return jb;
        }

        public override NodeBuilder GetArmatureAsset()
        {
            return _Joints
                .Select(item => item.Joints.Root)
                .Distinct()
                .FirstOrDefault();
        }

        public Transforms.IGeometryTransform GetWorldTransformer(string animationTrack, float time)
        {
            var jb = GetJointBindings();

            return new Transforms.SkinTransform
                (
                jb.Length,
                idx => jb[idx].InverseBindMatrix,
                idx => jb[idx].Joint.GetWorldMatrix(animationTrack, time),
                default, false
                );
        }

        #endregion
    }
}

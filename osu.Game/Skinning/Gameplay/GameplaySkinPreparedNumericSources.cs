// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    internal sealed class GameplaySkinPreparedNumericSources
    {
        internal IReadOnlyList<GameplaySkinNumericSource> Declarations { get; }
        private readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly GameplaySkinSceneStateFamily[] families;
        private readonly IReadOnlyList<GameplaySkinSceneBindingSource>[] dependencies;

        internal GameplaySkinPreparedNumericSources(IReadOnlyList<GameplaySkinNumericSource> declarations)
        {
            Declarations = declarations;
            families = new GameplaySkinSceneStateFamily[declarations.Count];
            dependencies = new IReadOnlyList<GameplaySkinSceneBindingSource>[declarations.Count];
            for (int i = 0; i < declarations.Count; i++)
            {
                GameplaySkinNumericSource declaration = declarations[i];
                indices.Add(declaration.Id, i);
                var sources = new HashSet<GameplaySkinSceneBindingSource>();
                include(declaration.Left, sources);
                if (declaration.Right != null)
                    include(declaration.Right, sources);
                dependencies[i] = sources.ToArray();
                families[i] = GameplaySkinSceneStateFamily.Layout;
                foreach (GameplaySkinSceneBindingSource source in sources)
                    families[i] |= GameplaySkinPreparedSceneProgram.StateFamilyFor(source);
            }
        }

        internal int IndexOf(string id) => indices[id];
        internal GameplaySkinSceneStateFamily StateFamily(string id) => families[IndexOf(id)];

        internal void IncludeDependencies(string id, ISet<GameplaySkinSceneBindingSource> destination)
        {
            foreach (GameplaySkinSceneBindingSource source in dependencies[IndexOf(id)])
                destination.Add(source);
        }

        private void include(GameplaySkinNumericOperand operand, ISet<GameplaySkinSceneBindingSource> destination)
        {
            if (operand.ReferenceIndex >= 0)
                foreach (GameplaySkinSceneBindingSource source in dependencies[operand.ReferenceIndex])
                    destination.Add(source);
            else if (!operand.Constant.HasValue)
                destination.Add(operand.Source);
        }
    }
}

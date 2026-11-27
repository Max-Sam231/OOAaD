using System.Text.Json;

namespace SpaceBattle.Lib
{
    public sealed class GameRulesParserBuilder
    {
        private readonly TopologicalOrderBuilder _sorter;

        public GameRulesParserBuilder(TopologicalOrderBuilder sorter)
        {
            ArgumentNullException.ThrowIfNull(sorter);
            _sorter = sorter;
        }

        public GameRulesConfig Parse(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Game rules JSON root must be an object.", nameof(json));
            }

            if (!root.TryGetProperty("version", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(versionElement.GetString()))
            {
                throw new ArgumentException("Game rules JSON must define a non-empty string 'version'.", nameof(json));
            }

            var version = versionElement.GetString()!;
            var victoryTarget = ReadVictoryTarget(root);
            var templates = ReadTemplates(root);
            var collisions = ReadCollisions(root);
            var entities = ReadEntities(root);
            var actions = ReadActions(root);

            return new GameRulesConfig(version, json, entities, actions, victoryTarget, templates, collisions);
        }

        private static string? ReadVictoryTarget(JsonElement root)
        {
            if (!root.TryGetProperty("victory_target", out var victoryTargetElement))
            {
                return null;
            }

            if (victoryTargetElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(victoryTargetElement.GetString()))
            {
                throw new ArgumentException("Optional 'victory_target' must be a non-empty string.");
            }

            return victoryTargetElement.GetString()!;
        }

        private static Dictionary<string, IReadOnlyDictionary<string, Dictionary<string, JsonElement>>> ReadTemplates(JsonElement root)
        {
            var templates = new Dictionary<string, IReadOnlyDictionary<string, Dictionary<string, JsonElement>>>(StringComparer.Ordinal);
            if (!root.TryGetProperty("templates", out var templatesElement))
            {
                return templates;
            }

            if (templatesElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Optional 'templates' must be a JSON object.");
            }

            foreach (var templateProp in templatesElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(templateProp.Name))
                {
                    throw new ArgumentException("Template name must not be empty or whitespace.");
                }

                var props = ReadPropertyMap(templateProp.Value, $"template '{templateProp.Name}'");
                if (!templates.TryAdd(templateProp.Name, props))
                {
                    throw new ArgumentException($"Duplicate template '{templateProp.Name}'.");
                }
            }

            return templates;
        }

        private static Dictionary<string, string> ReadCollisions(JsonElement root)
        {
            var collisions = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!root.TryGetProperty("collisions", out var collisionsElement))
            {
                return collisions;
            }

            if (collisionsElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Optional 'collisions' must be a JSON object.");
            }

            foreach (var pairProp in collisionsElement.EnumerateObject())
            {
                var parts = pairProp.Name.Split('.');
                if (parts.Length != 2
                    || string.IsNullOrWhiteSpace(parts[0])
                    || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new ArgumentException($"Invalid collision pair key '{pairProp.Name}'. Expected '<A>.<B>'.");
                }

                if (pairProp.Value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(pairProp.Value.GetString()))
                {
                    throw new ArgumentException($"Collision handler for '{pairProp.Name}' must be a non-empty string.");
                }

                var normalizedKey = string.CompareOrdinal(parts[0], parts[1]) <= 0
                    ? $"{parts[0]}.{parts[1]}"
                    : $"{parts[1]}.{parts[0]}";

                if (!collisions.TryAdd(normalizedKey, pairProp.Value.GetString()!))
                {
                    throw new ArgumentException($"Duplicate collision pair '{normalizedKey}'.");
                }
            }

            return collisions;
        }

        private static List<GameRuleEntity> ReadEntities(JsonElement root)
        {
            if (!root.TryGetProperty("entities", out var entitiesElement)
                || entitiesElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Game rules JSON must define an array 'entities'.");
            }

            var entities = new List<GameRuleEntity>();
            var seenEntityIds = new HashSet<int>();

            foreach (var entityElement in entitiesElement.EnumerateArray())
            {
                if (entityElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Each entry in 'entities' must be a JSON object.");
                }

                if (!entityElement.TryGetProperty("id", out var idElement)
                    || idElement.ValueKind != JsonValueKind.Number
                    || !idElement.TryGetInt32(out var id))
                {
                    throw new ArgumentException("Each entity must define an Int32 'id'.");
                }

                if (!seenEntityIds.Add(id))
                {
                    throw new ArgumentException($"Duplicate entity id '{id}'.");
                }

                if (!entityElement.TryGetProperty("properties", out var propertiesElement))
                {
                    throw new ArgumentException($"Entity '{id}' must define 'properties'.");
                }

                var properties = ReadPropertyMap(propertiesElement, $"entity '{id}' properties");
                entities.Add(new GameRuleEntity(id, properties));
            }

            return entities;
        }

        private List<GameRuleAction> ReadActions(JsonElement root)
        {
            if (!root.TryGetProperty("actions", out var actionsElement)
                || actionsElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Game rules JSON must define an object 'actions'.");
            }

            var actions = new List<GameRuleAction>();
            var actionNames = new List<string>();
            var actionNameSet = new HashSet<string>(StringComparer.Ordinal);

            foreach (var actionProp in actionsElement.EnumerateObject())
            {
                var actionName = actionProp.Name;
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    throw new ArgumentException("Action name must not be empty or whitespace.");
                }

                if (!actionNameSet.Add(actionName))
                {
                    throw new ArgumentException($"Duplicate action '{actionName}'.");
                }

                var actionBody = actionProp.Value;
                if (actionBody.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException($"Action '{actionName}' must be a JSON object.");
                }

                if (!actionBody.TryGetProperty("steps", out var stepsElement)
                    || stepsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new ArgumentException($"Action '{actionName}' must define an array 'steps'.");
                }

                var steps = new List<string>();
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    if (stepElement.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(stepElement.GetString()))
                    {
                        throw new ArgumentException($"Action '{actionName}' contains a non-string or empty step.");
                    }

                    steps.Add(stepElement.GetString()!);
                }

                if (steps.Count == 0)
                {
                    throw new ArgumentException($"Action '{actionName}' must contain at least one step.");
                }

                var orderConstraints = new List<string>();
                if (actionBody.TryGetProperty("order", out var orderElement))
                {
                    if (orderElement.ValueKind != JsonValueKind.Array)
                    {
                        throw new ArgumentException($"Action '{actionName}' field 'order' must be an array.");
                    }

                    foreach (var orderItem in orderElement.EnumerateArray())
                    {
                        if (orderItem.ValueKind != JsonValueKind.String
                            || string.IsNullOrWhiteSpace(orderItem.GetString()))
                        {
                            throw new ArgumentException($"Action '{actionName}' contains a non-string or empty order constraint.");
                        }

                        orderConstraints.Add(orderItem.GetString()!);
                    }
                }

                var orderedSteps = _sorter.SortActionSteps(actionName, steps, orderConstraints);
                actions.Add(new GameRuleAction(actionName, orderedSteps));
                actionNames.Add(actionName);
            }

            if (actionNames.Count > 0)
            {
                var crossEdges = new List<(string From, string To)>();
                foreach (var action in actions)
                {
                    foreach (var step in action.OrderedSteps)
                    {
                        if (step.StartsWith("Macro.", StringComparison.Ordinal))
                        {
                            var target = step.Substring("Macro.".Length);
                            if (actionNameSet.Contains(target))
                            {
                                crossEdges.Add((action.Name, target));
                            }
                        }
                    }
                }

                _sorter.Sort("actions", actionNames, crossEdges);
            }

            return actions;
        }

        private static Dictionary<string, Dictionary<string, JsonElement>> ReadPropertyMap(
            JsonElement propertiesElement,
            string context)
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"{context} must be a JSON object.");
            }

            var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
            foreach (var property in propertiesElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException($"Property '{property.Name}' in {context} must be a JSON object.");
                }

                var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var field in property.Value.EnumerateObject())
                {
                    fields[field.Name] = field.Value.Clone();
                }

                result[property.Name] = fields;
            }

            return result;
        }
    }
}

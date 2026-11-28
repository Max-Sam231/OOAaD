using System.Text.Json;

namespace SpaceBattle.Lib
{
    public sealed class GameRulesTextBuilder
    {
        private const string EntitiesPrefix = "В игре есть ";

        public string Translate(string rulesText, string version = "1.0.0")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rulesText);
            ArgumentException.ThrowIfNullOrWhiteSpace(version);

            var templates = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            var allowedByEntity = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var actions = new Dictionary<string, ActionDraft>(StringComparer.Ordinal);
            var orderConstraints = new List<(string NormA, string NormB, string Line)>();

            foreach (var rawLine in rulesText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith(EntitiesPrefix, StringComparison.Ordinal))
                {
                    foreach (var entity in ParseList(line.Substring(EntitiesPrefix.Length)))
                    {
                        templates.TryAdd(entity, new Dictionary<string, object>(StringComparer.Ordinal));
                        allowedByEntity.TryAdd(entity, new List<string>());
                    }
                }
                else if (TrySplitTwo(line, " может ", out var entityName, out var actionsText))
                {
                    if (!templates.ContainsKey(entityName))
                    {
                        throw new ArgumentException($"Unknown entity '{entityName}'.", nameof(rulesText));
                    }

                    var entityActions = allowedByEntity[entityName];
                    foreach (var actionName in ParseList(actionsText))
                    {
                        if (!entityActions.Contains(actionName, StringComparer.Ordinal))
                        {
                            entityActions.Add(actionName);
                        }

                        actions.TryAdd(
                            actionName,
                            new ActionDraft(new List<string> { NormalizeStep(actionName) }));
                    }
                }
                else if (TrySplitTwo(line, " включает ", out var actionName, out var stepsText))
                {
                    if (actions.TryGetValue(actionName, out var existing) && existing.HasExplicitSteps)
                    {
                        throw new ArgumentException($"Duplicate action '{actionName}'.", nameof(rulesText));
                    }

                    var rawSteps = ParseList(stepsText);
                    var normalizedSteps = new List<string>(rawSteps.Count);
                    foreach (var step in rawSteps)
                    {
                        normalizedSteps.Add(NormalizeStep(step));
                    }

                    actions[actionName] = new ActionDraft(normalizedSteps) { HasExplicitSteps = true };
                }
                else if (TrySplitTwo(line, " до ", out var firstStepText, out var secondStepText))
                {
                    orderConstraints.Add((NormalizeStep(firstStepText), NormalizeStep(secondStepText), line));
                }
                else
                {
                    throw new ArgumentException($"Unrecognized rule line '{line}'.", nameof(rulesText));
                }
            }

            foreach (var entry in allowedByEntity)
            {
                if (entry.Value.Count > 0)
                {
                    templates[entry.Key]["AllowedActions"] = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["type"] = "System.String",
                        ["value"] = string.Join(",", entry.Value)
                    };
                }
            }

            foreach (var item in orderConstraints)
            {
                var constraint = $"{item.NormA} before {item.NormB}";
                var matched = false;

                foreach (var action in actions.Values)
                {
                    if (action.Steps.Contains(item.NormA) && action.Steps.Contains(item.NormB))
                    {
                        action.Order.Add(constraint);
                        matched = true;
                    }
                }

                if (!matched)
                {
                    throw new ArgumentException($"Order constraint '{item.Line}' does not match any action.", nameof(rulesText));
                }
            }

            var serializedActions = new Dictionary<string, object>(actions.Count, StringComparer.Ordinal);
            foreach (var entry in actions)
            {
                serializedActions[entry.Key] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["steps"] = entry.Value.Steps,
                    ["order"] = entry.Value.Order
                };
            }

            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = version,
                ["templates"] = templates,
                ["entities"] = Array.Empty<object>(),
                ["actions"] = serializedActions
            };

            return JsonSerializer.Serialize(payload);
        }

        private static bool TrySplitTwo(string line, string separator, out string left, out string right)
        {
            var parts = line.Split(separator, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                left = parts[0].Trim();
                right = parts[1].Trim();
                return true;
            }

            left = string.Empty;
            right = string.Empty;
            return false;
        }

        private static List<string> ParseList(string rawList)
        {
            var parts = rawList.Split(',');
            var result = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var item = part.Trim();
                if (item.Length == 0)
                {
                    throw new ArgumentException($"Empty list element in '{rawList}'.");
                }

                result.Add(item);
            }

            return result;
        }

        private static string NormalizeStep(string step)
        {
            return step.Contains('.', StringComparison.Ordinal) ? step : "Commands." + step;
        }

        private sealed class ActionDraft
        {
            public List<string> Steps { get; }
            public List<string> Order { get; } = new List<string>();
            public bool HasExplicitSteps { get; set; }

            public ActionDraft(List<string> steps)
            {
                Steps = steps;
            }
        }
    }
}

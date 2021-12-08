﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `unevaluatedItems`.
	/// </summary>
	[Applicator]
	[SchemaPriority(30)]
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft201909)]
	[SchemaDraft(Draft.Draft202012)]
	[Vocabulary(Vocabularies.Applicator201909Id)]
	[Vocabulary(Vocabularies.Applicator202012Id)]
	[JsonConverter(typeof(UnevaluatedItemsKeywordJsonConverter))]
	public class UnevaluatedItemsKeyword : IJsonSchemaKeyword, IRefResolvable, ISchemaContainer, IEquatable<UnevaluatedItemsKeyword>
	{
		internal const string Name = "unevaluatedItems";

		/// <summary>
		/// The schema by which to validation unevaluated items.
		/// </summary>
		public JsonSchema Schema { get; }

		static UnevaluatedItemsKeyword()
		{
			ValidationContext.RegisterConsolidationMethod(ConsolidateAnnotations);
		}
		/// <summary>
		/// Creates a new <see cref="UnevaluatedItemsKeyword"/>.
		/// </summary>
		/// <param name="value">The schema by which to validation unevaluated items.</param>
		public UnevaluatedItemsKeyword(JsonSchema value)
		{
			Schema = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>
		/// Provides validation for the keyword.
		/// </summary>
		/// <param name="context">Contextual details for the validation process.</param>
		public void Validate(ValidationContext context, in JsonElement target, out ValidationResult result)
		{
			context.EnterKeyword(Name);
			if (target.ValueKind != JsonValueKind.Array)
			{
				context.WrongValueKind(target.ValueKind);
				result = ValidationResult.Success;
				return;
			}

			context.Options.LogIndentLevel++;
			int startIndex = 0;
			object? annotation;
			if (context.Options.ValidatingAs == Draft.Unspecified || context.Options.ValidatingAs.HasFlag(Draft.Draft202012))
			{
				annotation = context.TryGetAnnotation(PrefixItemsKeyword.Name);
				if (annotation != null)
				{
					context.Log(() => $"Annotation from {PrefixItemsKeyword.Name}: {annotation}.");
					if (annotation is bool) // is only ever true or a number
					{
						result = ValidationResult.Success;
						return;
					}
					startIndex = (int)annotation;
				}
				else
					context.Log(() => $"No annotations from {PrefixItemsKeyword.Name}.");
			}
			annotation = context.TryGetAnnotation(ItemsKeyword.Name);
			if (annotation != null)
			{
				context.Log(() => $"Annotation from {ItemsKeyword.Name}: {annotation}.");
				if (annotation is bool) // is only ever true or a number
				{
					result = ValidationResult.Success;
					return;
				}
				startIndex = (int) annotation;
			}
			else
				context.Log(() => $"No annotations from {ItemsKeyword.Name}.");
			annotation = context.TryGetAnnotation(AdditionalItemsKeyword.Name);
			if (annotation is bool) // is only ever true
			{
				context.Log(() => $"Annotation from {AdditionalItemsKeyword.Name}: {annotation}.");
				result = ValidationResult.Success;
				return;
			}
			context.Log(() => $"No annotations from {AdditionalItemsKeyword.Name}.");
			annotation = context.TryGetAnnotation(Name);
			if (annotation is bool) // is only ever true
			{
				context.Log(() => $"Annotation from {Name}: {annotation}.");
				result = ValidationResult.Success;
				return;
			}
			context.Log(() => $"No annotations from {Name}.");
			var indicesToValidate = Enumerable.Range(startIndex, target.GetArrayLength() - startIndex);
			if (context.Options.ValidatingAs.HasFlag(Draft.Draft202012) || context.Options.ValidatingAs == Draft.Unspecified)
			{
				annotation = context.TryGetAnnotation(ContainsKeyword.Name);
				if (annotation is List<int> validatedByContains)
				{
					context.Log(() => $"Annotation from {ContainsKeyword.Name}: {annotation}.");
					indicesToValidate = indicesToValidate.Except(validatedByContains);
				}
				else
					context.Log(() => $"No annotations from {ContainsKeyword.Name}.");
			}
			result = ValidationResult.Success;
			foreach (var i in indicesToValidate)
			{
				context.Log(() => $"Validating item at index {i}.");
				var item = target[i];
				//var subContext = ValidationContext.From(context,
				//	context.InstanceLocation.Combine(PointerSegment.Create($"{i}")),
				//	item);
				Schema.ValidateSubschema(context, in item, out var subResult);
				result.MergeAnd(in subResult);
				context.Log(() => $"Item at index {i} {subResult.IsValid.GetValidityString()}.");
				if (!result.IsValid && context.ApplyOptimizations) break;
				//context.NestedContexts.Add(subContext);
			}
			context.Options.LogIndentLevel--;

			if (result.IsValid)
				context.SetAnnotation(Name, true);
			context.ExitKeyword(Name, result.IsValid);
		}

		private static void ConsolidateAnnotations(IEnumerable<ValidationContext> sourceContexts, ValidationContext destContext)
		{
			if (sourceContexts.Select(c => c.TryGetAnnotation(Name)).OfType<bool>().Any())
				destContext.SetAnnotation(Name, true);
		}

		IRefResolvable? IRefResolvable.ResolvePointerSegment(string? value)
		{
			return value == null ? Schema : null;
		}

		void IRefResolvable.RegisterSubschemas(SchemaRegistry registry, Uri currentUri)
		{
			Schema.RegisterSubschemas(registry, currentUri);
		}

		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns>true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.</returns>
		public bool Equals(UnevaluatedItemsKeyword? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(Schema, other.Schema);
		}

		/// <summary>Determines whether the specified object is equal to the current object.</summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			return Equals(obj as UnevaluatedItemsKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Schema.GetHashCode();
		}
	}

	internal class UnevaluatedItemsKeywordJsonConverter : JsonConverter<UnevaluatedItemsKeyword>
	{
		public override UnevaluatedItemsKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options);

			return new UnevaluatedItemsKeyword(schema);
		}
		public override void Write(Utf8JsonWriter writer, UnevaluatedItemsKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(UnevaluatedItemsKeyword.Name);
			JsonSerializer.Serialize(writer, value.Schema, options);
		}
	}
}
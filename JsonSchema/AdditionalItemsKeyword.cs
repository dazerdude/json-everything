﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `additionalItems`.
	/// </summary>
	[Applicator]
	[SchemaPriority(10)]
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft6)]
	[SchemaDraft(Draft.Draft7)]
	[SchemaDraft(Draft.Draft201909)]
	[Vocabulary(Vocabularies.Applicator201909Id)]
	[JsonConverter(typeof(AdditionalItemsKeywordJsonConverter))]
	public class AdditionalItemsKeyword : IJsonSchemaKeyword, IRefResolvable, ISchemaContainer, IEquatable<AdditionalItemsKeyword>
	{
		internal const string Name = "additionalItems";

		/// <summary>
		/// The schema by which to validation additional items.
		/// </summary>
		public JsonSchema Schema { get; }

		static AdditionalItemsKeyword()
		{
			ValidationContext.RegisterConsolidationMethod(ConsolidateAnnotations);
		}
		/// <summary>
		/// Creates a new <see cref="AdditionalItemsKeyword"/>.
		/// </summary>
		/// <param name="value">The keyword's schema.</param>
		public AdditionalItemsKeyword(JsonSchema value)
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
			result = ValidationResult.Success;
			var annotation = context.TryGetAnnotation(ItemsKeyword.Name);
			if (annotation == null)
			{
				context.NotApplicable(() => $"No annotations from {ItemsKeyword.Name}.");
				result = ValidationResult.Success;
				return;
			}
			context.Log(() => $"Annotation from {ItemsKeyword.Name}: {annotation}.");
			if (annotation is bool)
			{
				result = ValidationResult.Success;
				context.ExitKeyword(Name, result.IsValid);
				return;
			}
			var startIndex = (int) annotation;

			for (int i = startIndex; i < target.GetArrayLength(); i++)
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
		public bool Equals(AdditionalItemsKeyword? other)
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
			return Equals(obj as AdditionalItemsKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Schema.GetHashCode();
		}
	}

	internal class AdditionalItemsKeywordJsonConverter : JsonConverter<AdditionalItemsKeyword>
	{
		public override AdditionalItemsKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options);

			return new AdditionalItemsKeyword(schema);
		}
		public override void Write(Utf8JsonWriter writer, AdditionalItemsKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(AdditionalItemsKeyword.Name);
			JsonSerializer.Serialize(writer, value.Schema, options);
		}
	}
}
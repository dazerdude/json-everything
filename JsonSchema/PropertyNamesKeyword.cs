﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.More;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `propertyNames`.
	/// </summary>
	[Applicator]
	[SchemaPriority(10)]
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft6)]
	[SchemaDraft(Draft.Draft7)]
	[SchemaDraft(Draft.Draft201909)]
	[SchemaDraft(Draft.Draft202012)]
	[Vocabulary(Vocabularies.Applicator201909Id)]
	[Vocabulary(Vocabularies.Applicator202012Id)]
	[JsonConverter(typeof(PropertyNamesKeywordJsonConverter))]
	public class PropertyNamesKeyword : IJsonSchemaKeyword, IRefResolvable, ISchemaContainer, IEquatable<PropertyNamesKeyword>
	{
		internal const string Name = "propertyNames";

		/// <summary>
		/// The schema to match.
		/// </summary>
		public JsonSchema Schema { get; }

		static PropertyNamesKeyword()
		{
			ValidationContext.RegisterConsolidationMethod(ConsolidateAnnotations);
		}

		/// <summary>
		/// Creates a new <see cref="PropertyNamesKeyword"/>.
		/// </summary>
		/// <param name="value">The schema to match.</param>
		public PropertyNamesKeyword(JsonSchema value)
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
			if (target.ValueKind != JsonValueKind.Object)
			{
				context.WrongValueKind(target.ValueKind);
				result = ValidationResult.Success;
				return;
			}

			context.Options.LogIndentLevel++;
			result = ValidationResult.Success;
			foreach (var name in target.EnumerateObject().Select(p => p.Name))
			{
				context.Log(() => $"Validating property name '{name}'.");
				var instance = name.AsJsonElement();
				//var subContext = ValidationContext.From(context,
				//	context.InstanceLocation.Combine(PointerSegment.Create($"{name}")),
				//	instance);
				Schema.ValidateSubschema(context, in instance, out var subResult);
				result.MergeAnd(in subResult);
				context.Log(() => $"Property name '{name}' {subResult.IsValid.GetValidityString()}.");
				if (!result.IsValid && context.ApplyOptimizations) break;
				//context.NestedContexts.Add(subContext);
			}
			context.Options.LogIndentLevel--;

			context.ExitKeyword(Name, result.IsValid);
		}

		private static void ConsolidateAnnotations(IEnumerable<ValidationContext> sourceContexts, ValidationContext destContext)
		{
			var allPropertyNames = sourceContexts.Select(c => c.TryGetAnnotation(Name))
				.Where(a => a != null)
				.Cast<List<string>>()
				.SelectMany(a => a)
				.Distinct()
				.ToList();
			// TODO: add message
			if (destContext.TryGetAnnotation(Name) is List<string> annotation)
				annotation.AddRange(allPropertyNames);
			else if (allPropertyNames.Any())
				destContext.SetAnnotation(Name, allPropertyNames);
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
		public bool Equals(PropertyNamesKeyword? other)
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
			return Equals(obj as PropertyNamesKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Schema.GetHashCode();
		}
	}

	internal class PropertyNamesKeywordJsonConverter : JsonConverter<PropertyNamesKeyword>
	{
		public override PropertyNamesKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options);

			return new PropertyNamesKeyword(schema);
		}
		public override void Write(Utf8JsonWriter writer, PropertyNamesKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(PropertyNamesKeyword.Name);
			JsonSerializer.Serialize(writer, value.Schema, options);
		}
	}
}
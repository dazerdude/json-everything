﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `not`.
	/// </summary>
	[Applicator]
	[SchemaPriority(20)]
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft6)]
	[SchemaDraft(Draft.Draft7)]
	[SchemaDraft(Draft.Draft201909)]
	[SchemaDraft(Draft.Draft202012)]
	[Vocabulary(Vocabularies.Applicator201909Id)]
	[Vocabulary(Vocabularies.Applicator202012Id)]
	[JsonConverter(typeof(NotKeywordJsonConverter))]
	public class NotKeyword : IJsonSchemaKeyword, IRefResolvable, ISchemaContainer, IEquatable<NotKeyword>
	{
		internal const string Name = "not";

		/// <summary>
		/// The schema to not match.
		/// </summary>
		public JsonSchema Schema { get; }

		/// <summary>
		/// Creates a new <see cref="NotKeyword"/>.
		/// </summary>
		/// <param name="value">The schema to not match.</param>
		public NotKeyword(JsonSchema value)
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
			//var subContext = ValidationContext.From(context,
			//	subschemaLocation: context.SchemaLocation.Combine(PointerSegment.Create(Name)));
			Schema.ValidateSubschema(context, in target, out result);
			//context.NestedContexts.Add(subContext);
			//result.IsValid = !subResult.IsValid;
			context.ConsolidateAnnotations();
			context.Options.LogIndentLevel++;
			//context.Log(() => $"Subschema {result.IsValid.GetValidityString()}.");
			context.Options.LogIndentLevel--;
			context.ExitKeyword(Name, result.IsValid);
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
		public bool Equals(NotKeyword? other)
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
			return Equals(obj as NotKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Schema.GetHashCode();
		}
	}

	internal class NotKeywordJsonConverter : JsonConverter<NotKeyword>
	{
		public override NotKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options);

			return new NotKeyword(schema);
		}
		public override void Write(Utf8JsonWriter writer, NotKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(NotKeyword.Name);
			JsonSerializer.Serialize(writer, value.Schema, options);
		}
	}
}
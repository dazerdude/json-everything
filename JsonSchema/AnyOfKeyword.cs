﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `anyOf`.
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
	[JsonConverter(typeof(AnyOfKeywordJsonConverter))]
	public class AnyOfKeyword : IJsonSchemaKeyword, IRefResolvable, ISchemaCollector, IEquatable<AnyOfKeyword>
	{
		internal const string Name = "anyOf";

		/// <summary>
		/// The keywords schema collection.
		/// </summary>
		public IReadOnlyList<JsonSchema> Schemas { get; }

		//public int Complexity { get; }

		/// <summary>
		/// Creates a new <see cref="AnyOfKeyword"/>.
		/// </summary>
		/// <param name="values">The set of schemas.</param>
		public AnyOfKeyword(params JsonSchema[] values)
			: this((IEnumerable<JsonSchema>)values ?? throw new ArgumentNullException(nameof(values))) { }

		/// <summary>
		/// Creates a new <see cref="AnyOfKeyword"/>.
		/// </summary>
		/// <param name="values">The set of schemas.</param>
		public AnyOfKeyword(IEnumerable<JsonSchema> values)
		{
			Schemas = values.ToList();//.OrderBy(s => s.Complexity).ToList();
			//Complexity = values.Sum(s => s.Complexity);
		}

		/// <summary>
		/// Provides validation for the keyword.
		/// </summary>
		/// <param name="context">Contextual details for the validation process.</param>
		public void Validate(ValidationContext context, in JsonElement target, out ValidationResult result)
		{
			context.EnterKeyword(Name);
			result = ValidationResult.Failure("Found no matches");
			for (var i = 0; i < Schemas.Count; i++)
			{
				context.Log(() => $"Processing {Name}[{i}]...");
				var schema = Schemas[i];
				schema.ValidateSubschema(context, in target, out var subResult);
				result.MergeOr(in subResult);
				if (result.IsValid && context.ApplyOptimizations) break;
			}

			if (result.IsValid)
				context.ConsolidateAnnotations();
			context.ExitKeyword(Name, result.IsValid);
		}

		IRefResolvable? IRefResolvable.ResolvePointerSegment(string? value)
		{
			if (!int.TryParse(value, out var index)) return null;
			if (index < 0 || Schemas.Count <= index) return null;

			return Schemas[index];
		}

		void IRefResolvable.RegisterSubschemas(SchemaRegistry registry, Uri currentUri)
		{
			foreach (var schema in Schemas)
			{
				schema.RegisterSubschemas(registry, currentUri);
			}
		}

		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns>true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.</returns>
		public bool Equals(AnyOfKeyword? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Schemas.ContentsEqual(other.Schemas);
		}

		/// <summary>Determines whether the specified object is equal to the current object.</summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			return Equals(obj as AnyOfKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Schemas.GetUnorderedCollectionHashCode();
		}
	}

	internal class AnyOfKeywordJsonConverter : JsonConverter<AnyOfKeyword>
	{
		public override AnyOfKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.StartArray)
			{
				var schemas = JsonSerializer.Deserialize<List<JsonSchema>>(ref reader, options);
				return new AnyOfKeyword(schemas);
			}
			
			var schema = JsonSerializer.Deserialize<JsonSchema>(ref reader, options);
			return new AnyOfKeyword(schema);
		}
		public override void Write(Utf8JsonWriter writer, AnyOfKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(AnyOfKeyword.Name);
			writer.WriteStartArray();
			foreach (var schema in value.Schemas)
			{
				JsonSerializer.Serialize(writer, schema, options);
			}
			writer.WriteEndArray();
		}
	}
}
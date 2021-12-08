﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `$ref`.
	/// </summary>
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft6)]
	[SchemaDraft(Draft.Draft7)]
	[SchemaDraft(Draft.Draft201909)]
	[SchemaDraft(Draft.Draft202012)]
	[Vocabulary(Vocabularies.Core201909Id)]
	[Vocabulary(Vocabularies.Core202012Id)]
	[JsonConverter(typeof(RefKeywordJsonConverter))]
	[SchemaPriority(100)]
	public class RefKeyword : IJsonSchemaKeyword, IEquatable<RefKeyword>
	{
		internal const string Name = "$ref";

		/// <summary>
		/// The URI reference.
		/// </summary>
		public Uri Reference { get; }

		public int Complexity { get; } = 1000;

		/// <summary>
		/// Creates a new <see cref="RefKeyword"/>.
		/// </summary>
		/// <param name="value">The URI reference.</param>
		public RefKeyword(Uri value)
		{
			Reference = value;
		}

		/// <summary>
		/// Provides validation for the keyword.
		/// </summary>
		/// <param name="context">Contextual details for the validation process.</param>
		public void Validate(ValidationContext context, in JsonElement target, out ValidationResult result)
		{
			context.EnterKeyword(Name);
			var parts = Reference.OriginalString.Split(new[] {'#'}, StringSplitOptions.None);
			var baseUri = parts[0];
			var fragment = parts.Length > 1 ? parts[1] : null;

			Uri? newUri;
			JsonSchema? baseSchema = null;
			if (!string.IsNullOrEmpty(baseUri))
			{
				if (Uri.TryCreate(baseUri, UriKind.Absolute, out newUri))
					baseSchema = context.Options.SchemaRegistry.Get(newUri);
				else if (context.CurrentUri != null)
				{
					var uriFolder = context.CurrentUri.OriginalString.EndsWith("/")
						? context.CurrentUri
						: context.CurrentUri.GetParentUri();
					newUri = new Uri(uriFolder, baseUri);
					baseSchema = context.Options.SchemaRegistry.Get(newUri);
				}
			}
			else
			{
				newUri = context.CurrentUri;
				baseSchema = context.Options.SchemaRegistry.Get(newUri) ?? context.SchemaRoot;
			}

			var absoluteReference = SchemaRegistry.GetFullReference(newUri, fragment);
			using var _ = context.NavigateToReference(in target, absoluteReference, out var foundRef);
			if (foundRef)
			{
				result = ValidationResult.Failure("Encountered recursive reference");
				context.ExitKeyword(Name, result.IsValid);
				return;
			}

			JsonSchema? schema;
			var navigatedByDirectRef = true;
			if (!string.IsNullOrEmpty(fragment) && AnchorKeyword.AnchorPattern.IsMatch(fragment!))
				schema = context.Options.SchemaRegistry.Get(newUri, fragment);
			else
			{
				if (baseSchema == null)
				{
					result = ValidationResult.Failure($"Could not resolve base URI `{newUri}`");
					context.ExitKeyword(Name, result.IsValid);
					return;
				}

				if (!string.IsNullOrEmpty(fragment))
				{
					fragment = $"#{fragment}";
					if (!JsonPointer.TryParse(fragment, out var pointer))
					{
						result = ValidationResult.Failure($"Could not parse pointer `{fragment}`");
						context.ExitKeyword(Name, result.IsValid);
						return;
					}

					(schema, newUri) = baseSchema.FindSubschema(pointer!, newUri);
					navigatedByDirectRef = false;
				}
				else
					schema = baseSchema;
			}

			if (schema == null)
			{
				result = ValidationResult.Failure($"Could not resolve reference `{Reference}`");
				context.ExitKeyword(Name, result.IsValid);
				return;
			}

			//var subContext = ValidationContext.From(context, newUri: newUri);
			//subContext.NavigatedByDirectRef = navigatedByDirectRef;
			//if (!string.IsNullOrEmpty(fragment) && JsonPointer.TryParse(fragment!, out var reference))
			//	subContext.Reference = reference;
			//if (!ReferenceEquals(baseSchema, context.SchemaRoot))
			//	subContext.SchemaRoot = baseSchema!;
			schema.ValidateSubschema(context, in target, out result);
			//context.NestedContexts.Add(subContext);
			context.ConsolidateAnnotations();
			//result.IsValid = subResult.IsValid;
			context.ExitKeyword(Name, result.IsValid);
		}

		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns>true if the current object is equal to the <paramref name="other">other</paramref> parameter; otherwise, false.</returns>
		public bool Equals(RefKeyword? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(Reference, other.Reference);
		}

		/// <summary>Determines whether the specified object is equal to the current object.</summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			return Equals(obj as RefKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Reference.GetHashCode();
		}
	}

	internal class RefKeywordJsonConverter : JsonConverter<RefKeyword>
	{
		public override RefKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var uri = reader.GetString(); 
			return new RefKeyword(new Uri(uri, UriKind.RelativeOrAbsolute));


		}
		public override void Write(Utf8JsonWriter writer, RefKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(RefKeyword.Name);
			JsonSerializer.Serialize(writer, value.Reference, options);
		}
	}

	// Source: https://github.com/WebDAVSharp/WebDAVSharp.Server/blob/1d2086a502937936ebc6bfe19cfa15d855be1c31/WebDAVExtensions.cs
}

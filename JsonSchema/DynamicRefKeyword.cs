﻿using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `$dynamicRef`.
	/// </summary>
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft202012)]
	[Vocabulary(Vocabularies.Core202012Id)]
	[JsonConverter(typeof(DynamicRefKeywordJsonConverter))]
	public class DynamicRefKeyword : IJsonSchemaKeyword, IEquatable<DynamicRefKeyword>
	{
		internal const string Name = "$dynamicRef";

		/// <summary>
		/// The URI reference.
		/// </summary>
		public Uri Reference { get; }

		/// <summary>
		/// Creates a new <see cref="DynamicRefKeyword"/>.
		/// </summary>
		/// <param name="value"></param>
		public DynamicRefKeyword(Uri value)
		{
			Reference = value ?? throw new ArgumentNullException(nameof(value));
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


			JsonSchema? GetSchema(Uri? uri, string? anchor)
			{
				var uriScope = uri ?? context.CurrentUri;
				var currentScopeDefinesDynamicAnchor =
					!string.IsNullOrEmpty(fragment) &&
					context.Options.SchemaRegistry.DynamicScopeDefinesAnchor(uriScope!, fragment!);

				return currentScopeDefinesDynamicAnchor
					? context.Options.SchemaRegistry.GetDynamic(uri, anchor)
					: context.Options.SchemaRegistry.Get(uri, anchor);
			}

			Uri? newUri;
			JsonSchema? baseSchema = null;
			if (!string.IsNullOrEmpty(baseUri))
			{
				if (Uri.TryCreate(baseUri, UriKind.Absolute, out newUri))
					baseSchema = GetSchema(newUri, fragment);
				else if (context.CurrentUri != null)
				{
					var uriFolder = context.CurrentUri.OriginalString.EndsWith("/")
						? context.CurrentUri
						: context.CurrentUri.GetParentUri();
					newUri = new Uri(uriFolder, baseUri);
					baseSchema = GetSchema(newUri, fragment);
				}
			}
			else
			{
				newUri = context.CurrentUri;
				baseSchema ??= GetSchema(newUri, fragment) ?? context.SchemaRoot;
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
			if (!string.IsNullOrEmpty(fragment) && AnchorKeyword.AnchorPattern.IsMatch(fragment!))
			{
				schema = baseSchema ?? GetSchema(newUri, fragment);
			}
			else
			{
				if (baseSchema == null)
				{
					result = ValidationResult.Failure($"Could not resolve base URI `{baseUri}`");
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
				}
				else
					schema = baseSchema;
			}

			if (schema == null)
			{
				result = ValidationResult.Failure($"Could not resolve DynamicReference `{Reference}`");
				context.ExitKeyword(Name, result.IsValid);
				return;
			}

            //var subContext = ValidationContext.From(context, newUri: newUri);
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
		public bool Equals(DynamicRefKeyword? other)
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
			return Equals(obj as DynamicRefKeyword);
		}

		/// <summary>Serves as the default hash function.</summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			return Reference != null ? Reference.GetHashCode() : 0;
		}
	}

	internal class DynamicRefKeywordJsonConverter : JsonConverter<DynamicRefKeyword>
	{
		public override DynamicRefKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var uri = reader.GetString(); 
			return new DynamicRefKeyword(new Uri(uri, UriKind.RelativeOrAbsolute));


		}
		public override void Write(Utf8JsonWriter writer, DynamicRefKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(DynamicRefKeyword.Name);
			JsonSerializer.Serialize(writer, value.Reference, options);
		}
	}
}

﻿using Json.More;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Json.Schema
{
	public struct ValidationResult
	{
		public static readonly ValidationResult Success = new ValidationResult(true, false, null);
		public static readonly ValidationResult Ignored = new ValidationResult(true, true, null);

		public bool IsValid { get; private set; }
		public bool IsIgnored { get; private set; }
		public List<string>? Errors { get; private set; }
		public List<string> RootErrorPath { get; private set; }

		private ValidationResult(bool isValid, bool isIgnored, List<string>? errors) : this()
			=> (IsValid, IsIgnored, Errors) = (isValid, isIgnored, errors);

		public static ValidationResult Check(bool isValid, string errorMessage) => isValid ? Success : Failure(errorMessage);
		public static ValidationResult Failure(string msg) => new ValidationResult(false, false, new List<string> { msg });

		public void AnnotateError(string name)
        {
			if (IsValid) { return; }
			RootErrorPath ??= new List<string>();
			RootErrorPath.Add(name);
        }

		public void Recheck(bool isValid, string errorMessage)
		{
			IsValid = isValid;
			if (!isValid)
			{
				Errors ??= new List<string>();
				Errors.Insert(0, errorMessage);
			}
		}

		public void MergeOr(in ValidationResult other) => Merge(in other, (l, r) => l | r);
		public void MergeAnd(in ValidationResult other) => Merge(in other, (l, r) => l & r);
		public void Merge(in ValidationResult other, Func<bool, bool, bool> mergeFunc)
		{
			if (other.IsIgnored) { return; }
			IsValid = mergeFunc(IsValid, other.IsValid);

			if (!other.IsValid && other.Errors != null)
            {
				if (Errors == null) { Errors = other.Errors; }
				else { Errors.AddRange(other.Errors); }
				RootErrorPath ??= other.RootErrorPath;
            }
        }
    }

	/// <summary>
	/// Provides a single source of data for validation operations.
	/// </summary>
	public class ValidationContext
	{
		/// <summary>
		/// Consolidates properties from multiple child contexts onto a single parent context.
		/// Generally, a keyword will define how it handles its own consolidation.  This action
		/// must be registered on startup.
		/// </summary>
		/// <param name="sourceContexts">the source (child) contexts.</param>
		/// <param name="destContext">The destination (parent) context.</param>
		public delegate void ContextConsolidator(IList<ValidationContext> sourceContexts, ValidationContext destContext);

		//private static readonly List<ContextConsolidator> _consolidationActions = new List<ContextConsolidator>();

		//private Dictionary<string, Annotation>? _annotations;
		//private List<ValidationContext>? _nestedContexts;
		//private List<ValidationContext>? _siblingContexts;
		//private Dictionary<string, JsonSchema>? _dynamicAnchors;
		//private JsonSchema? _currentAnchorBackup;
		//private bool _isConsolidating;

		/// <summary>
		/// Indicates whether the validation passed or failed.
		/// </summary>
		//public bool IsValid { get; set; }
		/// <summary>
		/// Indicates whether this context should be ignored in the validation output.  (e.g. `$defs`)
		/// </summary>
		//public bool Ignore { get; set; }
		/// <summary>
		/// Gets or sets error message.
		/// </summary>
		//public string? Message { get; set; }

		/// <summary>
		/// The collection of annotations collected during the validation pass.
		/// </summary>
		//public IReadOnlyCollection<Annotation> Annotations => (_annotations ??= new Dictionary<string, Annotation>()).Values;
		/// <summary>
		/// The collection of validation contexts from nested schemas.
		/// </summary>
		/// <remarks>
		/// This property is lazy and will create a value upon first access.  To check
		/// whether there are any nested contexts, use <see cref="HasNestedContexts"/>.
		/// </remarks>
		//public List<ValidationContext> NestedContexts => _nestedContexts ??= new List<ValidationContext>();
		/// <summary>
		/// The collection of validation contexts of sibling keywords that have already been processed.
		/// </summary>
		/// <remarks>
		/// This property is lazy and will create a value upon first access.  To check
		/// whether there are any nested contexts, use <see cref="HasSiblingContexts"/>.
		/// </remarks>
		//public List<ValidationContext> SiblingContexts => _siblingContexts ??= new List<ValidationContext>();

		/// <summary>
		/// The option set for the validation.
		/// </summary>
		public ValidationOptions Options { get; private set; }
		/// <summary>
		/// The root schema.
		/// </summary>
		public JsonSchema SchemaRoot { get; internal set; }
		/// <summary>
		/// The current subschema location relative to the schema root.
		/// </summary>
		//public JsonPointer SchemaLocation { get; internal set; }
		/// <summary>
		/// The current subschema.
		/// </summary>
		public JsonSchema LocalSchema { get; internal set; }
		/// <summary>
		/// The current instance location relative to the instance root.
		/// </summary>
		//public JsonPointer InstanceLocation { get; internal set; }
		/// <summary>
		/// The current instance.
		/// </summary>
		//public JsonElement LocalInstance { get; internal set; }
		/// <summary>
		/// The current URI, based on `$id` and `$anchor` keywords present in the schema.
		/// </summary>
		public Uri? CurrentUri { get; internal set; }
		/// <summary>
		/// The current URI anchor.
		/// </summary>J
		public JsonSchema? CurrentAnchor { get; internal set; }
		/// <summary>
		/// (Obsolete) Get the set of defined dynamic anchors.
		/// </summary>
		//[Obsolete("This is no longer used. Dynamic anchors are tracked with the registry now.")]
		//public Dictionary<string, JsonSchema> DynamicAnchors => _dynamicAnchors ??= new Dictionary<string, JsonSchema>();

		//internal bool UriChanged { get; set; }
		//internal ValidationContext ParentContext { get; set; }
		//internal JsonPointer? Reference { get; set; }
		//internal IReadOnlyDictionary<Uri, bool>? MetaSchemaVocabs { get; set; }
		//internal bool IsNewDynamicScope { get; set; }
		private Dictionary<string, HashSet<JsonElement>> NavigatedReferences { get; } = new Dictionary<string, HashSet<JsonElement>>();
		//internal bool NavigatedByDirectRef { get; set; }

		public IDisposable NavigateToReference(in JsonElement element, string str, out bool found)
        {
			if (!NavigatedReferences.TryGetValue(str, out var set))
            {
				NavigatedReferences[str] = set = new HashSet<JsonElement>(JsonElementEqualityComparer.Instance);
            }
			found = !set.Add(element);
			return new PopReferenceDisp(this, str, in element);
        }

        private class PopReferenceDisp : IDisposable
        {
            private ValidationContext Ctx { get; }
            private string Target { get; }
			private JsonElement Element { get; }

			public PopReferenceDisp(ValidationContext ctx, string str, in JsonElement elem) => (Ctx, Target, Element) = (ctx, str, elem);
			public void Dispose() => Ctx.NavigatedReferences[Target].Remove(Element);
        }

        /// <summary>
        /// Whether processing optimizations can be applied (output format = flag).
        /// </summary>
        public bool ApplyOptimizations => Options.OutputFormat == OutputFormat.Flag;
		/// <summary>
		/// Whether the context has any nested contexts.
		/// </summary>
		//public bool HasNestedContexts => _nestedContexts != null && _nestedContexts.Count != 0;
		/// <summary>
		/// Whether the context has any sibling contexts.
		/// </summary>
		//public bool HasSiblingContexts => _siblingContexts != null && _siblingContexts.Count != 0;

#pragma warning disable 8618
		internal ValidationContext(ValidationOptions options)
		{
			Options = options;
		}
#pragma warning restore 8618

		/// <summary>
		/// Creates a new context from an existing one.  Use this for subschema validations.
		/// </summary>
		/// <param name="source">The source context.</param>
		/// <param name="instanceLocation">(optional) Updates the instance location.</param>
		/// <param name="instance">(optional) Updates the instance location.</param>
		/// <param name="subschemaLocation">(optional) Updates the subschema location.</param>
		/// <param name="newUri">(optional) Updates the current URI.</param>
		/// <returns></returns>
		/*
		public static ValidationContext From(ValidationContext source,
		                                     in JsonPointer? instanceLocation = null,
		                                     in JsonElement? instance = null,
		                                     in JsonPointer? subschemaLocation = null,
		                                     Uri? newUri = null)
		{
			return new ValidationContext(source.Options)
			{
				SchemaRoot = source.SchemaRoot,
				SchemaLocation = subschemaLocation ?? source.SchemaLocation,
				LocalSchema = source.LocalSchema,
				InstanceLocation = instanceLocation ?? source.InstanceLocation,
				LocalInstance = instance ?? source.LocalInstance,
				_currentAnchorBackup = source.CurrentAnchor,
				CurrentAnchor = source.CurrentAnchor,
				CurrentUri = newUri ?? source.CurrentUri,
				Reference = source.Reference,
				UriChanged = source.UriChanged || source.CurrentUri != newUri,
				_navigatedReferences = source._navigatedReferences == null || instance != null
					? null
					: new HashSet<string>(source._navigatedReferences),
				MetaSchemaVocabs = source.MetaSchemaVocabs
			};
		}

		internal void ImportAnnotations(ValidationContext? context)
		{
			_annotations = context?._annotations;
		}

		internal void ValidateAnchor()
		{
			CurrentAnchor = _currentAnchorBackup;
		}
		*/

		/// <summary>
		/// Invokes all consolidation actions.  Should be called at the end of processing an applicator keyword.
		/// </summary>
		public void ConsolidateAnnotations()
		{
			/*
			if (!HasNestedContexts) return;
			foreach (var consolidationAction in _consolidationActions)
			{
				_isConsolidating = true;
				consolidationAction(NestedContexts, this);
				_isConsolidating = false;
			}
			*/
		}

		/// <summary>
		/// Sets an annotation.
		/// </summary>
		/// <param name="owner">The annotation key.  Typically the name of the keyword.</param>
		/// <param name="value">The annotation value.</param>
		public void SetAnnotation(string owner, object value)
		{
			//_annotations ??= new Dictionary<string, Annotation>();
			//_annotations[owner] = new Annotation(owner, value, SchemaLocation) {WasConsolidated = _isConsolidating};
		}

		/// <summary>
		/// Tries to get an annotation.
		/// </summary>
		/// <param name="key">The annotation key.</param>
		/// <returns>The annotation or null.</returns>
		public object? TryGetAnnotation(string key)
		{
			//if (_annotations == null) return null;
			//return _annotations.TryGetValue(key, out var annotation) ? annotation.Value : null;
			return null;
		}

		/// <summary>
		/// Registers a consolidation action.
		/// </summary>
		/// <param name="consolidateAnnotations">The action.</param>
		public static void RegisterConsolidationMethod(ContextConsolidator consolidateAnnotations)
		{
			//_consolidationActions.Add(consolidateAnnotations);
		}

		//internal IEnumerable<Type>? GetKeywordsToProcess()
		//{
		//	return MetaSchemaVocabs?.Keys
		//		.SelectMany(x => Options.VocabularyRegistry.Get(x)?.Keywords ??
		//		                 Enumerable.Empty<Type>());
		//}
	}
}
// Regenerates the strongly-typed accessors in Strings.Designer.cs from Strings.resx.
//
// `dotnet build` does not run the Visual Studio resx generator, so a resource added to
// the .resx has no accessor until this runs, and a removed resource keeps a stale one
// (with a now-wrong summary comment). Rewriting the whole accessor list from the resx
// keeps the file consistent in both directions.
//
// The class scaffolding (header, ResourceManager, Culture, closing braces) is taken
// from the committed version of the file, so the output stays byte-compatible with what
// the Visual Studio generator would produce.
//
// Usage: node tools/sync-strings-designer.js   (run from the Bloxstrap folder)

const fs = require("fs");

const resxPath = "Resources/Strings.resx";
const designerPath = "Resources/Strings.Designer.cs";

const resxRaw = fs.readFileSync(resxPath, "utf8");

// A .resx opens with the standard Microsoft schema as an XML comment, and that comment
// itself contains example <data> elements. Strip comments first, or they get parsed as
// real resources.
const resx = resxRaw.replace(/<!--[\s\S]*?-->/g, "");

// Collect <data name="X"><value>Y</value></data>
//
// Bound each entry by its </data> rather than pairing <value> with </value> directly:
// a self-closing <value /> has no closing tag, and a lazy value match would then run on
// to the *next* entry's </value>, swallowing it and every entry after it.
const entries = [];
const dataRe = /<data\s+name="([^"]+)"[^>]*>([\s\S]*?)<\/data>/g;
let m;
while ((m = dataRe.exec(resx)) !== null) {
  const body = m[2];

  let value;
  if (/<value\s*\/>\s*$/.test(body.trimEnd()) || /<value\s*\/>/.test(body)) {
    // <value /> - a present but empty resource.
    value = "";
  } else {
    const valueMatch = /<value(?:\s[^>]*)?>([\s\S]*?)<\/value>/.exec(body);
    if (!valueMatch) {
      console.warn(`warning: no <value> found for resource ${m[1]}, skipping`);
      continue;
    }
    value = valueMatch[1];
  }

  entries.push({ name: m[1], value });
}

const toPropertyName = (resxName) => resxName.replace(/\./g, "_");

// A .resx may legitimately contain several names that collapse to the same identifier
// once dots become underscores. Keep the first, matching the resx, and say so loudly
// rather than silently dropping one.
const byProp = new Map();
const collisions = [];
for (const entry of entries) {
  const prop = toPropertyName(entry.name);
  if (byProp.has(prop)) {
    collisions.push(`${prop} (from ${byProp.get(prop).name} and ${entry.name})`);
    continue;
  }
  byProp.set(prop, entry);
}

if (collisions.length) {
  console.warn(`warning: ${collisions.length} resource name(s) collapse to the same property:`);
  collisions.forEach((c) => console.warn(`  ${c}`));
}

const designer = fs.readFileSync(designerPath, "utf8");

// Everything up to the first generated accessor is the hand-stable scaffolding.
const firstAccessor = designer.search(
  /^[ \t]*\/\/\/ <summary>\r?\n[ \t]*\/\/\/   Looks up a localized string similar to /m,
);

if (firstAccessor < 0)
  throw new Error(
    "could not find the first accessor in Strings.Designer.cs; it looks hand-edited or truncated",
  );

const prefix = designer.slice(0, firstAccessor);

// The generator escapes the summary text for XML. It also collapses runs of whitespace,
// because many resx values are authored across several lines and a raw newline inside a
// /// comment produces a file that will not compile.
const escapeSummary = (s) =>
  s
    .replace(/\s+/g, " ")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    // never let a value terminate the doc comment early
    .replace(/\*\//g, "*\\/");

const props = [...byProp.keys()].sort((a, b) => a.localeCompare(b, "en"));

const blocks = props.map((prop) => {
  const entry = byProp.get(prop);
  return [
    "        /// <summary>",
    `        ///   Looks up a localized string similar to ${escapeSummary(entry.value)}.`,
    "        /// </summary>",
    `        public static string ${prop} {`,
    "            get {",
    `                return ResourceManager.GetString("${entry.name}", resourceCulture);`,
    "            }",
    "        }",
  ].join("\r\n");
});

// The generated file uses CRLF, so match on the line ending actually present rather
// than assuming one.
const crlf = designer.includes("\r\n");
const nl = crlf ? "\r\n" : "\n";
const indent = crlf ? "        \r\n" : "        \n";

const body = blocks.join(indent) + nl;
const updated = prefix.replace(/\s*$/, "") + nl + indent + body + "    }" + nl + "}" + nl;

fs.writeFileSync(designerPath, updated, "utf8");

console.log(
  `regenerated ${props.length} accessors from ${entries.length} resx entries` +
    (collisions.length ? ` (${collisions.length} collapsed)` : ""),
);

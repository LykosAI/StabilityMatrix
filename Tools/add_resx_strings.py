# /// script
# requires-python = ">=3.10"
# dependencies = [
#     "pyperclip",
#     "rich",
#     "typer",
# ]
# ///
"""
Script to add new localization strings to .resx files from JSON data
provided either via a file or the system clipboard.

Uses standard library xml.etree.ElementTree with a CommentedTreeBuilder
for parsing (to check existing keys while preserving comment info).
Uses Regex and text manipulation to insert new entries, aiming for
minimal changes to the original file formatting.
"""

import json
import os
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, Tuple, Optional, List

import typer
import pyperclip  # For clipboard access
from rich.console import Console
from rich.table import Table
from rich.panel import Panel

# Initialize Typer app and Rich console
app = typer.Typer(help="Adds new localization strings to .resx files from JSON using text insertion.")
console = Console()

# --- Custom Tree Builder to handle comments ---
# This allows parsing comments, primarily so the parser doesn't fail
# on them and we can still build a tree to check existing keys.
class CommentedTreeBuilder(ET.TreeBuilder):
    def comment(self, data):
        # We don't strictly need to store comments in the tree for this script's logic,
        # but this prevents the parser from erroring out.
        # You could store them if needed using ET.Comment element type.
        pass # Simply ignore comments during tree building for key checking

# --- Helper Functions ---

def get_indentation(line: str) -> str:
    """Extracts leading whitespace (indentation) from a line."""
    match = re.match(r"^(\s*)", line)
    return match.group(1) if match else ""

def escape_xml_value(value: str) -> str:
    """Escapes special XML characters in the string value."""
    value = value.replace("&", "&amp;") # Must be first
    value = value.replace("<", "&lt;")
    value = value.replace(">", "&gt;")
    value = value.replace("'", "&apos;")
    value = value.replace("\"", "&quot;")
    return value

# --- Core Logic ---

def add_strings_to_resx(
    resx_file_path: Path, 
    strings_to_add: Dict[str, str],
    add_comment: str | None = None,
) -> Tuple[int, int, bool]:
    """
    Parses a .resx file minimally to check keys, then uses text manipulation
    and regex to insert new <data> elements, preserving original formatting.

    Args:
        resx_file_path: Path to the .resx file.
        strings_to_add: Dictionary of {key: value} strings to add.
        add_comment: Optional comment to add to the new data elements.

    Returns:
        A tuple containing (added_count, skipped_count, success_boolean).
    """
    if not resx_file_path.exists():
        console.print(f"  [[yellow]Skipped[/]]: File not found.")
        return 0, 0, False

    added_count = 0
    skipped_count = 0
    changes_made = False

    try:
        # Read the original file content
        original_content = resx_file_path.read_text(encoding="utf-8")

        # 1. Parse minimally to find existing keys (ignore comments in tree)
        parser = ET.XMLParser(target=CommentedTreeBuilder())
        try:
            # Feed the content to the parser
            parser.feed(original_content)
            root = parser.close() # Get the root element
            existing_keys = {data.get("name") for data in root.findall("./data")}
        except ET.ParseError as e:
            # If parsing fails even with CommentedTreeBuilder, report and exit for this file
            console.print(
                f"  [[bold red]XML Parse Error[/]]: Failed basic structure check for {resx_file_path.name}: {e}"
            )
            return 0, 0, False # Treat as failure

        # 2. Filter strings to add only new keys
        new_strings = {
            key: value for key, value in strings_to_add.items() if key not in existing_keys
        }
        skipped_count = len(strings_to_add) - len(new_strings)

        if not new_strings:
            console.print(
                f"  [[bold blue]No Changes[/]]: No new keys to add. Skipped [yellow]{skipped_count}[/] keys."
            )
            return 0, skipped_count, True # No changes needed, but operation was successful

        # 3. Prepare new XML snippets as strings
        new_elements_xml: List[str] = []
        base_indentation = "  " # Default/fallback indentation
        last_data_line_indent = None

        # Try to find indentation from existing data elements
        lines = original_content.splitlines()
        for line in reversed(lines):
             stripped_line = line.strip()
             if stripped_line.startswith("<data name="):
                 last_data_line_indent = get_indentation(line)
                 break
             elif stripped_line.startswith("<resheader name="): # Check resheader as fallback
                 last_data_line_indent = get_indentation(line)
                 break

        if last_data_line_indent is not None:
            base_indentation = last_data_line_indent
        else:
             # If no data/resheader found, look for root indent + 2 spaces? Or keep default.
             for line in lines:
                  if "<root>" in line:
                       base_indentation = get_indentation(line) + "  "
                       break
             console.print(f"  [dim]Could not detect indentation from <data> tags, using default '{base_indentation}'.[/dim]")


        for key, value in new_strings.items():
            escaped_value = escape_xml_value(value)
            # Manually format the XML string to match typical .resx style
            xml_snippet = (
                f'{base_indentation}<data name="{key}" xml:space="preserve">\n'
                f'{base_indentation}  <value>{escaped_value}</value>\n'
                f'{base_indentation}  <comment>{add_comment}</comment>\n' if add_comment else ''
                f'{base_indentation}</data>'
            )
            new_elements_xml.append(xml_snippet)
            added_count += 1
            changes_made = True

        # 4. Find insertion point using Regex
        insertion_pos = -1
        # Regex to find the end of the last </data> tag
        # We search backwards from the end of the file for efficiency
        last_data_match = None
        for match in re.finditer(r"</data>\s*$", original_content, re.MULTILINE):
             last_data_match = match

        if last_data_match:
            insertion_pos = last_data_match.end()
            # Ensure insertion happens *after* the newline following the tag
            if insertion_pos < len(original_content) and original_content[insertion_pos] == '\n':
                 insertion_pos += 1
            elif insertion_pos < len(original_content) and original_content[insertion_pos:insertion_pos+2] == '\r\n':
                 insertion_pos += 2 # Handle Windows line endings
            else:
                 # If </data> is the very last thing, add a newline before inserting
                 new_elements_xml.insert(0, "") # Add newline before first new element

            console.print(f"  [dim]Found last </data> tag. Inserting new elements after line {original_content[:insertion_pos].count(chr(10)) + 1}.[/dim]")

        else:
            # Fallback: Insert before the closing </root> tag
            root_close_match = re.search(r"</root>\s*$", original_content, re.MULTILINE)
            if root_close_match:
                insertion_pos = root_close_match.start()
                # Add a newline if the line before </root> isn't empty
                prev_char_index = insertion_pos - 1
                while prev_char_index >= 0 and original_content[prev_char_index] in (' ', '\t', '\r', '\n'):
                    prev_char_index -= 1
                if prev_char_index >= 0 and original_content[prev_char_index] != '\n':
                     new_elements_xml.insert(0, "") # Add newline before first new element

                console.print(f"  [dim]No </data> tags found. Inserting new elements before </root> (line {original_content[:insertion_pos].count(chr(10)) + 1}).[/dim]")
            else:
                # Very unlikely fallback: append to the end (might break XML structure)
                console.print("  [[bold red]Error[/]]: Could not find last </data> or </root> tag. Cannot determine insertion point.")
                return added_count, skipped_count, False

        # 5. Construct the new file content
        new_content = (
            original_content[:insertion_pos]
            + "\n".join(new_elements_xml) + ("\n" if insertion_pos != len(original_content) and not original_content[insertion_pos:].startswith(("\n", "\r")) else "") # Add newline separator if needed
            + original_content[insertion_pos:]
        )

        # 6. Write the modified content back to the file
        try:
            resx_file_path.write_text(new_content, encoding="utf-8")
            console.print(
                f"  [[bold green]Success[/]]: Added [cyan]{added_count}[/] keys via text insertion. Skipped [yellow]{skipped_count}[/] keys. File updated."
            )
            return added_count, skipped_count, True
        except IOError as e:
            console.print(
                f"  [[bold red]IO Error[/]]: Failed writing updated content to {resx_file_path.name}: {e}"
            )
            return added_count, skipped_count, False


    except ET.ParseError as e: # Catch errors during the initial key check parse
        console.print(
            f"  [[bold red]XML Error[/]]: Failed to parse {resx_file_path.name} for key checking: {e}"
        )
        return added_count, skipped_count, False
    except IOError as e:
        console.print(
            f"  [[bold red]IO Error[/]]: Failed reading/writing {resx_file_path.name}: {e}"
        )
        return added_count, skipped_count, False
    except Exception as e:
        console.print(
            f"  [[bold red]Unexpected Error[/]] processing {resx_file_path.name}: {e}"
        )
        import traceback
        traceback.print_exc()
        return added_count, skipped_count, False


@app.command()
def main(
    languages_dir: Path = typer.Argument(
        "./StabilityMatrix.Avalonia/Languages",
        help="Path to the directory containing the .resx files (e.g., ./Languages)",
        exists=True,
        file_okay=False,
        dir_okay=True,
        readable=True,
        resolve_path=True,
    ),
    json_input_file: Optional[Path] = typer.Option(
        None,
        "--json-file",
        "-f",
        help="Path to the JSON file containing the strings to add.",
        exists=True,
        file_okay=True,
        dir_okay=False,
        readable=True,
        resolve_path=True,
    ),
    from_clipboard: bool = typer.Option(
        False,
        "--clipboard",
        "-c",
        help="Read JSON data directly from the system clipboard instead of a file.",
    ),
):
    """
    Adds new localization strings to .RESX files from JSON data (file or clipboard).
    """
    all_strings: Optional[ResxData] = None

    # --- Input Validation and Loading ---
    if from_clipboard and json_input_file:
        console.print("[bold red]Error:[/bold red] Cannot use --json-file and --clipboard simultaneously.")
        raise typer.Exit(code=1)
    if from_clipboard:
        console.print("Attempting to read JSON from clipboard...")
        try:
            clipboard_content = pyperclip.paste()
            if not clipboard_content:
                console.print("[bold red]Error:[/bold red] Clipboard is empty.")
                raise typer.Exit(code=1)
            all_strings = json.loads(clipboard_content)
            console.print("[green]Successfully parsed JSON from clipboard.[/green]")
        except pyperclip.PyperclipException as e:
            console.print(f"[bold red]Clipboard Error:[/bold red] Could not access clipboard: {e}")
            raise typer.Exit(code=1)
        except json.JSONDecodeError as e:
            console.print(f"[bold red]JSON Error:[/bold red] Failed to decode JSON from clipboard: {e}")
            raise typer.Exit(code=1)
        except Exception as e:
            console.print(f"[bold red]Error:[/bold red] An unexpected error occurred reading from clipboard: {e}")
            raise typer.Exit(code=1)
    elif json_input_file:
        console.print(f"Reading JSON from file: [blue]{json_input_file}[/blue]")
        try:
            with open(json_input_file, "r", encoding="utf-8") as f:
                all_strings = json.load(f)
            console.print("[green]Successfully parsed JSON from file.[/green]")
        except json.JSONDecodeError as e:
            console.print(f"[bold red]JSON Error:[/bold red] Failed to decode JSON file {json_input_file}: {e}")
            raise typer.Exit(code=1)
        except IOError as e:
            console.print(f"[bold red]IO Error:[/bold red] Failed reading JSON file {json_input_file}: {e}")
            raise typer.Exit(code=1)
        except Exception as e:
            console.print(f"[bold red]Error:[/bold red] An unexpected error occurred reading JSON file: {e}")
            raise typer.Exit(code=1)
    else:
        console.print("[bold red]Error:[/bold red] Please provide either --json-file or --clipboard.")
        raise typer.Exit(code=1)
    if not all_strings:
        console.print("[bold red]Error:[/bold red] No JSON data loaded.")
        raise typer.Exit(code=1)

    # --- Processing Logic ---
    console.print(f"\nStarting localization update in: [blue]{languages_dir}[/blue]")
    results = []
    any_changes_made_across_files = False
    for lang_code, strings_for_lang in all_strings.items():
        lang_code_lower = lang_code.strip().lower() if isinstance(lang_code, str) else ""
        if lang_code_lower in ("base", "en", ""):
             resx_filename = "Resources.resx"
        else:
             resx_filename = f"Resources.{lang_code.strip()}.resx"
        resx_file_path = languages_dir / resx_filename
        console.print(f"\nProcessing language: [magenta]{lang_code}[/] ({resx_file_path.name})")
        if not isinstance(strings_for_lang, dict):
            console.print(f"  [[bold red]Data Error[/]]: Expected a dictionary of strings for language '{lang_code}', but got {type(strings_for_lang)}. Skipping.")
            results.append((resx_file_path.name, "[bold red]Invalid Data Format[/]", 0, 0))
            continue
            
        # For non-base languages, add a 'Fuzzy' comment
        add_comment = "Fuzzy" if lang_code_lower not in ("base", "en", "") else None
            
        added, skipped, success = add_strings_to_resx(resx_file_path, strings_for_lang, add_comment=add_comment)
        if success and added > 0:
            any_changes_made_across_files = True
        status = "[bold green]Success[/]"
        if not success:
             if not resx_file_path.exists():
                 status = "[yellow]Not Found[/]"
             else:
                 status = "[bold red]Failed[/]"
        elif added == 0:
             status = "[bold blue]No Changes[/]"
        results.append((resx_file_path.name, status, added, skipped))

    # --- Summary Report ---
    console.print("\n[bold]Localization Update Summary:[/bold]")
    summary_table = Table(show_header=True, header_style="bold magenta")
    summary_table.add_column("Language File", style="dim cyan", width=35)
    summary_table.add_column("Status")
    summary_table.add_column("Keys Added", justify="right")
    summary_table.add_column("Keys Skipped", justify="right")
    total_added = 0
    total_skipped = 0
    total_failed_or_missing = 0
    for filename, status, added, skipped in results:
        summary_table.add_row(filename, status, str(added), str(skipped))
        total_added += added
        total_skipped += skipped
        if "Failed" in status or "Not Found" in status or "Invalid Data" in status:
            total_failed_or_missing += 1
    console.print(summary_table)
    console.print(f"\nTotal Keys Added: [bold green]{total_added}[/]")
    console.print(f"Total Keys Skipped (Already Exist): [bold yellow]{total_skipped}[/]")
    if total_failed_or_missing > 0:
        console.print(f"Files Failed, Not Found, or Invalid Data: [bold red]{total_failed_or_missing}[/]")
    if not any_changes_made_across_files and total_failed_or_missing == 0:
         console.print("\n[bold blue]No changes were made to any files.[/bold blue]")
    else:
         console.print("\n[bold green]Update complete![/bold green]")


if __name__ == "__main__":
    app()

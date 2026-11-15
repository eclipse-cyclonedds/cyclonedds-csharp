# Copyright(c) 2026 Sven Trittler
#
# This program and the accompanying materials are made available under the
# terms of the Eclipse Public License v. 2.0 which is available at
# http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
# v. 1.0 which is available at
# http://www.eclipse.org/org/documents/edl-v10.php.
#
# SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
from datetime import datetime
import json
from pathlib import Path

manual_dir = Path(__file__).resolve().parent
variables = json.loads((manual_dir / "variables.json").read_text())
variables["copyright"] = variables["copyright"].replace(":year:", str(datetime.now().year))
variables["version"] = variables["release"] = (manual_dir.parents[1] / "VERSION").read_text().strip()

project = variables["project"]
author = variables["author"]
copyright = variables["copyright"]
version = variables["version"]
release = variables["release"]

needs_sphinx = "6.0"
extensions = []
root_doc = "index"
language = "en"
exclude_patterns = ["_build", "Thumbs.db", ".DS_Store"]
highlight_language = "csharp"

try:
    import piccolo_theme
except ImportError:
    html_theme = "alabaster"
else:
    extensions.append("piccolo_theme")
    html_theme = "piccolo_theme"

html_title = f"{project}, {version}"
html_short_title = html_title
rst_epilog = "\n".join(f".. |var-{key}| replace:: {value}" for key, value in variables.items())

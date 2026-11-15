# Build the documentation

From the repository root:

```sh
python3 -m venv build/docs-venv
. build/docs-venv/bin/activate
python -m pip install -r docs/requirements.txt
python -m sphinx -W --keep-going -b html docs/manual build/docs
python -m http.server --directory build/docs
```

Open <http://localhost:8000>. On Windows, activate the environment with
`build/docs-venv/Scripts/Activate.ps1`.

The layout follows the C++ binding: reStructuredText pages under `docs/manual/`,
with `conf.py`, `variables.json`, `api/` and `howtos/`. The website builder discovers
`conf.py` and runs Sphinx. Publishing also requires a C# project entry in the
website repository's `config.json`.

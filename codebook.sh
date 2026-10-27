#!/usr/bin/env bash
# Opdracht 3 - codeboek genereren op je Linux machine.
# 50 lijnen van 40 willekeurige tekens (A-Z, 0-9, _ = spatie), genummerd.
# /dev/urandom is cryptografisch veilig; Math.random() of Python random NIET.
tr -dc 'A-Z0-9_' < /dev/urandom | fold -w 40 | head -n 50 | nl -w2 -s': '

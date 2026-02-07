#!/usr/bin/env bash
set -e

# Run this script to update translation files in 'po' directory.

# To add new langauges run 'msginit --locale=fi --input=Stocks.pot'
# instead in po directory. Replace 'fi' with locale you want to add.

DOMAIN="Stocks"
VERSION="0.1.0"
PO_DIR="po"
POTFILES="$PO_DIR/POTFILES"
POT_FILE="$PO_DIR/$DOMAIN.pot"

echo "Updating Stocks.pot to include all translatable strings of files listed in POTFILES..."

xgettext \
  --from-code=UTF-8 \
  --package-name="$DOMAIN" \
  --package-version="$VERSION" \
  --language=C# \
  --keyword=_ \
  --keyword=C_:1c,2 \
  --output="$POT_FILE" \
  --files-from="$POTFILES"

echo "Update all existing PO files with new translatable strings..."

for po in "$PO_DIR"/*.po; do
  echo "$po"
  msgmerge --update --backup=none "$po" "$POT_FILE"
done

echo "Done!"

set dotenv-load
export VINTAGE_STORY := env("VINTAGE_STORY", "/var/lib/flatpak/app/at.vintagestory.VintageStory/x86_64/stable/active/files/extra/vintagestory")

build:
    dotnet build PapermakingPapyrus.slnx

format:
    dotnet format PapermakingPapyrus.slnx

lint:
    dotnet format PapermakingPapyrus.slnx --verify-no-changes

test:
    dotnet test PapermakingPapyrus.slnx

validate: lint test release

release:
    scripts/package.sh

clean:
    dotnet clean PapermakingPapyrus.slnx

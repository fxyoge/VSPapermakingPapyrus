#!/usr/bin/env perl

use strict;
use warnings;
use JSON::PP;
use Digest::SHA qw(sha1_hex);
use File::Basename qw(fileparse);

@ARGV == 2 or die
    "Usage: perl tools/vs-shape-to-bbmodel.pl <shape.json> <model.bbmodel>\n";
my ($input_path, $output_path) = @ARGV;

open my $input, "<", $input_path or die "Cannot read $input_path: $!\n";
local $/;
my $source = <$input>;
close $input;

# VS shape JSON permits unquoted object keys. Quote those keys so JSON::PP can
# parse it without adding a third-party JSON5 dependency.
$source =~ s/([{,])(\s*)([A-Za-z_]\w*)\s*:/$1$2"$3":/g;
my $shape = decode_json($source);

my @texture_names = sort keys %{ $shape->{textures} // {} };
my %texture_indices;
@texture_indices{@texture_names} = (0 .. $#texture_names);
my @face_names = qw(north east south west up down);
my $uuid_counter = 0;

sub uuid {
    my $hex = sha1_hex(join(":", time, $$, rand(), $uuid_counter++));
    return join("-", substr($hex, 0, 8), substr($hex, 8, 4),
        substr($hex, 12, 4), substr($hex, 16, 4), substr($hex, 20, 12));
}

sub default_uv {
    my ($element) = @_;
    my ($x1, $y1, $z1) = @{ $element->{from} };
    my ($x2, $y2, $z2) = @{ $element->{to} };
    my $horizontal = ($x2 - $x1) > ($z2 - $z1)
        ? ($x2 - $x1) : ($z2 - $z1);
    my $vertical = ($y2 - $y1) > ($z2 - $z1)
        ? ($y2 - $y1) : ($z2 - $z1);
    return [0, 0, $horizontal, $vertical];
}

sub convert_face {
    my ($face, $element) = @_;
    (my $texture_name = $face->{texture} // "") =~ s/^#//;
    my $result = {
        uv => $face->{uv} // default_uv($element),
        texture => $texture_indices{$texture_name} // 0,
    };
    $result->{rotation} = $face->{rotation} if exists $face->{rotation};
    $result->{enabled} = $face->{enabled} if exists $face->{enabled};
    return $result;
}

my @elements;
for my $index (0 .. $#{ $shape->{elements} // [] }) {
    my $element = $shape->{elements}[$index];
    my %faces;
    for my $name (@face_names) {
        $faces{$name} = convert_face($element->{faces}{$name}, $element)
            if exists $element->{faces}{$name};
    }
    my $converted = {
        name => $element->{name} // "cube" . ($index + 1),
        box_uv => JSON::PP::false,
        render_order => "default",
        locked => JSON::PP::false,
        shade => $element->{shade} // JSON::PP::true,
        export => JSON::PP::true,
        from => $element->{from},
        to => $element->{to},
        autouv => 1,
        color => $index % 8,
        origin => $element->{rotationOrigin} // [8, 8, 8],
        faces => \%faces,
        type => "cube",
        uuid => uuid(),
    };
    my $rotation = [
        $element->{rotationX} // 0,
        $element->{rotationY} // 0,
        $element->{rotationZ} // 0,
    ];
    $converted->{rotation} = $rotation if grep { $_ != 0 } @$rotation;
    push @elements, $converted;
}

my $root_uuid = uuid();
my $texture_width = $shape->{textureWidth} // 16;
my $texture_height = $shape->{textureHeight} // 16;
my ($model_name) = fileparse($output_path, qr/\.[^.]+/);
my @textures;
for my $index (0 .. $#texture_names) {
    my $name = $texture_names[$index];
    push @textures, {
        name => $name,
        relative_path => "",
        folder => "",
        namespace => "",
        id => "$index",
        width => $texture_width,
        height => $texture_height,
        uv_width => $texture_width,
        uv_height => $texture_height,
        particle => JSON::PP::false,
        use_as_default => $index == 0 ? JSON::PP::true : JSON::PP::false,
        render_mode => "default",
        render_sides => "auto",
        textureLocation => $shape->{textures}{$name},
        visible => JSON::PP::true,
        internal => JSON::PP::false,
        saved => JSON::PP::true,
        uuid => uuid(),
    };
}

my $model = {
    meta => {
        format_version => "5.0",
        model_format => "formatVS",
        box_uv => JSON::PP::false,
    },
    name => $model_name,
    visible_box => [1, 1, 0],
    variable_placeholders => "",
    resolution => { width => $texture_width, height => $texture_height },
    elements => \@elements,
    groups => [{
        name => "root",
        uuid => $root_uuid,
        export => JSON::PP::true,
        locked => JSON::PP::false,
        origin => [8, 8, 8],
        rotation => [0, 0, 0],
        children => [],
        visibility => JSON::PP::true,
        autouv => 0,
        isOpen => JSON::PP::true,
    }],
    outliner => [{
        uuid => $root_uuid,
        isOpen => JSON::PP::true,
        children => [map { $_->{uuid} } @elements],
    }],
    textures => \@textures,
};

open my $output, ">", $output_path or die "Cannot write $output_path: $!\n";
print {$output} JSON::PP->new->canonical->pretty->encode($model);
close $output;
print "Converted " . scalar(@elements) . " elements to $output_path\n";

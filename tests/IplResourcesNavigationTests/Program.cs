using System;

static void Check(bool value){if(!value)throw new Exception("IPL resource navigation failed");}
var selection=new IplSelection();
Check(selection.Category==0&&selection.Count==3&&selection.Selected==0);
selection.Move(-1);Check(selection.Selected==2);selection.Move(1);Check(selection.Selected==0);
selection.NextCategory(1);selection.Move(-1);
Check(selection.Category==1&&selection.Selected==9&&selection.FirstVisible==6);
selection.NextCategory(1);selection.Move(1);selection.Move(-1);
Check(selection.Category==2&&selection.Count==0&&selection.Selected==0);
selection.NextCategory(1);Check(selection.Category==3&&selection.Count==0);
selection.NextCategory(1);selection.SetSampleCount(12);
for(int i=0;i<12;i++)
{
    Check(selection.Selected==i&&selection.Selected>=selection.FirstVisible&&selection.Selected<selection.FirstVisible+IplSelection.VisibleRows);
    selection.Move(1);
}
Check(selection.Selected==0);selection.Move(-1);Check(selection.Selected==11&&selection.FirstVisible==6);
selection.NextCategory(1);Check(selection.Category==0&&selection.Selected==0);
selection.NextCategory(1);Check(selection.Category==1&&selection.Selected==9);
selection.NextCategory(-1);selection.NextCategory(-1);Check(selection.Selected==11);
selection.SetSampleCount(3);Check(selection.Count==3&&selection.Selected==0);
selection.SetSampleCount(0);selection.Move(-1);Check(selection.Count==0&&selection.Selected==0);
int originalTexture=selection.Texture;
for(int i=0;i<IplSelection.Textures.Length;i++)selection.NextTexture();
Check(selection.Texture==originalTexture);
Console.WriteLine("IPL categories, empty groups, independent selection, six-row scrolling and texture navigation passed");

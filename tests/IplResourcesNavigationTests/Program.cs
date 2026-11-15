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
var complete=new IplSelection();complete.SetCompleteAudio(true);complete.SetSampleCount(12);
complete.NextCategory(1);complete.Move(-1);
Check(complete.Count==12&&complete.Selected==11&&complete.FirstVisible==6&&IplSelection.GetName(1,11)=="Error");
complete.NextCategory(1);complete.Move(-1);
Check(complete.Count==7&&complete.Selected==6&&complete.FirstVisible==6&&IplSelection.GetName(2,6)=="Move to A done");
complete.NextCategory(1);complete.Move(-1);
Check(complete.Count==5&&complete.Selected==4&&IplSelection.GetName(3,4)=="Mono output");
complete.SetCompleteAudio(false);Check(complete.Count==0&&complete.Selected==0);
complete.NextCategory(-1);Check(complete.Count==0&&complete.Selected==0);
complete.NextCategory(-1);Check(complete.Count==10&&complete.Selected==0);
Console.WriteLine("IPL categories, empty groups, independent selection, six-row scrolling and texture navigation passed");

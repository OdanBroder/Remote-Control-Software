   M S B A M L   `   `   `  ����A  =Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null5clr-namespace:Client.ViewModelsClient.ViewModels  5   	       Client.Views.MainView   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   1localclr-namespace:Client.ViewModels     5   	   X TFontAwesome.Sharp, Version=6.6.0.0, Culture=neutral, PublicKeyToken=d16d1e4e568ec10fFfa=http://schemas.awesome.incremented/wpf/xaml/fontawesome.sharp  5   	     � Title$  MainViewModel��5	   	   $	��700��6   $
��1300��6,    � WindowStartupLocation$ CenterScreen=�5
   	    � WindowStyle$
 None=�5   	   
������  � AllowsTransparency . .��5      ��  �� CornerRadius$ 10��6   $��1q�6   ��5   
   �� 5       �� 
StartPoint$	 0,0&�6"    �� EndPoint$	 1,1&�63   .��5      �    winBorderColor1 � Color# [  6    � Offset$ 0Q�6H   � 5        winBorderColor2# [ 6   $	 0.5Q�6H   � 5        winBorderColor3# [ 6   $ 1Q�6H   5      ._�5   
   � 	 � ColumnDefinitions	 5      �� 5      $	��250 �6#   �� 5      $��* �6#   .+�5!      �� $ 10, 0, 0, 10��6   $��0��61   ��5"      �� 5#      $
 0, 0&�6*   $ 1, 0.7&�6<   .��5$      �   secondaryBackColor1# [ 6'   $ 0Q�6T   � 5%        secondaryBackColor2# [ 6'   $	 0.7Q�6T   5&      ._�5)      �� .+�5+      �� 
 �� Orientation$
 
Horizontal=�6!   $��35��5,   !   $��15, 25, 0, 40q�5-   !   .+�5.      �� $��/Images/logo.png��6    $��30��6:   �� 5/      $��MyApp��6$     titleColor2#��[ 50   $   $��20(�51   $   $��
Montserrat)�52   $   $��Medium"�53   $   $��Center=�54   $   $��10, 0, 0, 0q�55   $   56      57      58      �� 5;      $ 0, 10, 10, 0��6   $��1��61   ��5<      �� 5=      $
 1, 1&�6*   $
 0, 0&�6<   .��5>      � # [ 6'   $ 0Q�6T   � 5?      # [ 6'   $	 0.7Q�6T   5@      5B      	� 5E        
menuButton#��[ 6    
 color1
 � Tag# [ 5F      $��
0,99,0,549q�68   .��5G      �� $
 
Horizontal=�6   .+�5H      !  FontAwesome.Sharp.IconImage  �  �FontAwesome.Sharp.IconImageBase`1[[FontAwesome.Sharp.IconChar, FontAwesome.Sharp, Version=6.6.0.0, Culture=neutral, PublicKeyToken=d16d1e4e568ec10f]]   Icon$ 	Handshake=�6#     menuButtonIcon#��[ 64   �� 5I      $��Connect��6     	 menuButtonText#��[	 6/   5J      5K      	� 5L      #��[ 6   # [ 5M      $��0,149,0,499q�6+   .��5N      �� $
 
Horizontal=�6   .+�5O        $ History=�6#   #��[ 62   �� 5P      $��File ��6    #��[	 6-   5Q      5R      	� 5S      #��[ 6   # [ 5T      $��0,199,0,449q�6,   .��5U      �� $
 
Horizontal=�6   .+�5V        $ History=�6#   #��[ 62   �� 5W      $��History��6    #��[	 6/   5X      5Y      5Z      5]      5^      